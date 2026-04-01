using VisualSqlArchitect.Nodes.Compilers;
using VisualSqlArchitect.Registry;

namespace VisualSqlArchitect.Nodes;

/// <summary>
/// Walks a <see cref="NodeGraph"/> and resolves each node into an
/// <see cref="ISqlExpression"/> tree.
///
/// The compiler is stateless per-graph: create one instance, call <see cref="Compile"/>.
/// Thread-safe once constructed (the EmitContext is immutable).
///
/// Resolution is recursive and memoised — each node is compiled exactly once
/// regardless of how many downstream nodes reference its output pin.
/// </summary>
public sealed class NodeGraphCompiler(NodeGraph graph, EmitContext ctx)
{
    private readonly EmitContext _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
    private readonly NodeGraph _graph = graph ?? throw new ArgumentNullException(nameof(graph));
    private readonly NodeCompilerFactory _factory = new NodeCompilerFactory();

    // Memoisation: nodeId → compiled expression
    private readonly Dictionary<string, ISqlExpression> _cache = [];

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Compiles the entire graph and returns a <see cref="CompiledNodeGraph"/>
    /// ready for the <see cref="QueryEngine.QueryGeneratorService"/>.
    /// </summary>
    public CompiledNodeGraph Compile()
    {
        // Warm the memo cache in topological order (no redundant recomputes)
        foreach (NodeInstance node in _graph.TopologicalOrder())
            Resolve(node.Id, "result"); // "result" is the default output pin name

        // ── SELECT expressions ────────────────────────────────────────────────
        var selects = _graph
            .SelectOutputs.Select(b =>
                (Resolve(b.NodeId, b.PinName), b.Alias ?? GetNodeAlias(b.NodeId))
            )
            .ToList();

        // ── WHERE expressions ─────────────────────────────────────────────────
        IReadOnlyList<ISqlExpression> wheres = BuildWhereExpressions();

        // ── HAVING expressions ────────────────────────────────────────────────
        IReadOnlyList<ISqlExpression> havings = BuildHavingExpressions();

        // ── QUALIFY expressions (post-window filter) ────────────────────────
        IReadOnlyList<ISqlExpression> qualifies = BuildQualifyExpressions();

        // ── ORDER BY ──────────────────────────────────────────────────────────
        var orders = _graph
            .OrderBys.Select(b => (Resolve(b.NodeId, b.PinName), b.Descending))
            .ToList();

        // ── GROUP BY ──────────────────────────────────────────────────────────
        var groups = _graph.GroupBys.Select(b => Resolve(b.NodeId, b.PinName)).ToList();

        return new CompiledNodeGraph(
            selects,
            wheres,
            havings,
            qualifies,
            orders,
            groups,
            _graph.Distinct,
            _graph.Limit,
            _graph.Offset
        );
    }

    // ── Node resolution (memo + dispatch) ────────────────────────────────────

    /// <summary>
    /// Returns the compiled expression for <paramref name="nodeId"/>'s named pin.
    /// Results are memoised per node (output pin is implicit for single-output nodes).
    /// </summary>
    private ISqlExpression Resolve(string nodeId, string pinName = "result")
    {
        string cacheKey = $"{nodeId}::{pinName}";
        if (_cache.TryGetValue(cacheKey, out ISqlExpression? cached))
            return cached;

        NodeInstance node = _graph.NodeMap[nodeId];
        ISqlExpression expr = CompileNode(node, pinName);

        _cache[cacheKey] = expr;
        return expr;
    }

    /// <summary>Resolves an input pin: wire → upstream node, or literal fallback.</summary>
    private ISqlExpression ResolveInput(
        string nodeId,
        string pinName,
        PinDataType expectedType = PinDataType.Expression
    )
    {
        Connection? wire = _graph.GetSingleInputConnection(nodeId, pinName);
        if (wire is not null)
            return Resolve(wire.FromNodeId, wire.FromPinName);

        // No wire — check for a literal value in the node's PinLiterals dict
        NodeInstance node = _graph.NodeMap[nodeId];
        if (node.PinLiterals.TryGetValue(pinName, out string? literal))
            return BuildLiteral(literal, expectedType);

        // Optional pin with no value → NULL
        return NullExpr.Instance;
    }

    /// <summary>
    /// Resolves a multi-input pin (AND, OR gates).
    /// Returns all expressions connected to the pin.
    /// </summary>
    private IReadOnlyList<ISqlExpression> ResolveMultiInput(string nodeId, string pinName)
    {
        IReadOnlyList<Connection> wires = _graph.GetInputConnections(nodeId, pinName);
        return wires.Select(w => Resolve(w.FromNodeId, w.FromPinName)).ToList();
    }

    // ── Node resolution context ──────────────────────────────────────────────

    /// <summary>
    /// Context passed to compilers. Provides access to graph resolution, emit context,
    /// and access to the graph structure itself.
    /// </summary>
    private sealed class CompilationContext(NodeGraphCompiler compiler) : INodeCompilationContext
    {
        private readonly NodeGraphCompiler _compiler = compiler;

        public NodeGraph Graph => _compiler._graph;
        public EmitContext EmitContext => _compiler._ctx;

        public ISqlExpression Resolve(string nodeId, string pinName = "result") =>
            _compiler.Resolve(nodeId, pinName);

        public ISqlExpression ResolveInput(
            string nodeId,
            string pinName,
            PinDataType expectedType = PinDataType.Expression
        ) => _compiler.ResolveInput(nodeId, pinName, expectedType);

        public IReadOnlyList<ISqlExpression> ResolveInputs(string nodeId, string pinName) =>
            _compiler.ResolveMultiInput(nodeId, pinName);
    }

    // ── Node dispatch (using factory) ─────────────────────────────────────────

    private ISqlExpression CompileNode(NodeInstance node, string pinName)
    {
        var context = new CompilationContext(this);
        return _factory.Compile(node, context, pinName);
    }

    // ── WHERE combination ─────────────────────────────────────────────────────

    private IReadOnlyList<ISqlExpression> BuildWhereExpressions()
    {
        if (_graph.WhereConditions.Count == 0)
            return [];
        if (_graph.WhereConditions.Count == 1)
            return [Resolve(_graph.WhereConditions[0].NodeId, _graph.WhereConditions[0].PinName)];

        // Build a tree that respects the LogicOp of each binding
        var result = new List<ISqlExpression>();
        foreach (WhereBinding binding in _graph.WhereConditions)
        {
            ISqlExpression expr = Resolve(binding.NodeId, binding.PinName);
            result.Add(expr);
        }

        return result;
    }

    private IReadOnlyList<ISqlExpression> BuildHavingExpressions()
    {
        if (_graph.Havings.Count == 0)
            return [];

        return _graph.Havings.Select(b => Resolve(b.NodeId, b.PinName)).ToList();
    }

    private IReadOnlyList<ISqlExpression> BuildQualifyExpressions()
    {
        if (_graph.Qualifies.Count == 0)
            return [];

        return _graph.Qualifies.Select(b => Resolve(b.NodeId, b.PinName)).ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ISqlExpression ResolveInputOrParam(
        NodeInstance node,
        string pinName,
        string? defaultLiteral,
        PinDataType type
    )
    {
        Connection? wire = _graph.GetSingleInputConnection(node.Id, pinName);
        if (wire is not null)
            return Resolve(wire.FromNodeId, wire.FromPinName);

        if (node.PinLiterals.TryGetValue(pinName, out string? pinLit))
            return BuildLiteral(pinLit, type);

        if (node.Parameters.TryGetValue(pinName, out string? param))
            return BuildLiteral(param, type);

        return defaultLiteral is null ? NullExpr.Instance : BuildLiteral(defaultLiteral, type);
    }

    private static ISqlExpression BuildLiteral(string raw, PinDataType type) =>
        type switch
        {
            PinDataType.Number
                when double.TryParse(
                    raw,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double d
                ) => new NumberLiteralExpr(d),

            PinDataType.Text => new StringLiteralExpr(raw),

            _ => new LiteralExpr(raw, type),
        };

    private string? GetNodeAlias(string nodeId) =>
        _graph.NodeMap.TryGetValue(nodeId, out NodeInstance? node) ? node.Alias : null;

    private static ISqlExpression CompileValueDateTime(NodeInstance node)
    {
        string value = node.Parameters.TryGetValue("value", out string? v) ? v : "";

        if (string.IsNullOrWhiteSpace(value))
            return NullExpr.Instance;

        // Treat as a string literal (date formats vary by provider)
        // Providers will interpret ISO format or provider-specific datetime strings
        return new StringLiteralExpr(value);
    }

    private static ISqlExpression CompileValueBoolean(NodeInstance node)
    {
        string value = node.Parameters.TryGetValue("value", out string? b) ? b : "true";

        // Normalize to provider-specific boolean literal
        string normalized = value.ToLowerInvariant().Trim();
        if (normalized is "true" or "1" or "yes" or "on")
            return new LiteralExpr("TRUE", PinDataType.Boolean);
        else if (normalized is "false" or "0" or "no" or "off")
            return new LiteralExpr("FALSE", PinDataType.Boolean);

        return new LiteralExpr(normalized, PinDataType.Boolean);
    }

    private ISqlExpression CompileLogicGate(NodeInstance node, LogicOperator op)
    {
        // Each condition has its own dynamic input pin (cond_1, cond_2, …).
        // Chaining AND/OR nodes automatically produces nested parenthesised SQL
        // because LogicGateExpr.Emit wraps multi-operand expressions in ( … ).
        var conditions = _graph
            .Connections.Where(c => c.ToNodeId == node.Id && c.ToPinName.StartsWith("cond_"))
            .OrderBy(c => c.ToPinName, StringComparer.Ordinal)
            .Select(c => Resolve(c.FromNodeId, c.FromPinName))
            .ToList();

        return new LogicGateExpr(op, conditions);
    }

    private ISqlExpression CompileColumnList(NodeInstance node)
    {
        // Each column has its own dynamic input pin (col_1, col_2, …).
        // Resolve them in pin-name order. The actual SELECT binding list is built by
        // LiveSqlBarViewModel directly from canvas connections, so the return value
        // here is used only for graph-warmup; returning the first column is sufficient.
        var columns = _graph
            .Connections.Where(c => c.ToNodeId == node.Id && c.ToPinName.StartsWith("col_"))
            .OrderBy(c => c.ToPinName, StringComparer.Ordinal)
            .Select(c => Resolve(c.FromNodeId, c.FromPinName))
            .ToList();

        return columns.Count switch
        {
            0 => NullExpr.Instance,
            _ => columns[0],
        };
    }

    private ISqlExpression CompileCompileWhere(NodeInstance node)
    {
        // Resolve all conditions from the multi-input "conditions" pin
        IReadOnlyList<ISqlExpression> conditions = ResolveMultiInput(node.Id, "conditions");

        // If no conditions, return TRUE
        if (conditions.Count == 0)
            return new LiteralExpr("TRUE", PinDataType.Boolean);

        // If exactly one condition, return it directly
        if (conditions.Count == 1)
            return conditions[0];

        // Multiple conditions: combine with AND
        return new LogicGateExpr(LogicOperator.And, conditions);
    }
}
