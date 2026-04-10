using DBWeaver.Nodes.Compilers;
using DBWeaver.Registry;

namespace DBWeaver.Nodes;

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
public sealed class NodeGraphCompiler(
    NodeGraph graph,
    EmitContext ctx,
    bool allowLegacyBindings = true)
{
    private readonly EmitContext _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
    private readonly NodeGraph _graph = graph ?? throw new ArgumentNullException(nameof(graph));
    private readonly bool _allowLegacyBindings = allowLegacyBindings;
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

        NodeInstance? outputSink = ResolvePrimaryOutputSink();

        // ── SELECT expressions ────────────────────────────────────────────────
        IReadOnlyList<SelectBinding> selectBindings = BuildSelectBindings(outputSink);
        var selects = selectBindings
            .Select(b =>
                (Resolve(b.NodeId, b.PinName), b.Alias)
            )
            .ToList();

        // ── WHERE expressions ─────────────────────────────────────────────────
        IReadOnlyList<ISqlExpression> wheres = BuildWhereExpressions(outputSink);

        // ── HAVING expressions ────────────────────────────────────────────────
        IReadOnlyList<ISqlExpression> havings = BuildHavingExpressions(outputSink);

        // ── QUALIFY expressions (post-window filter) ────────────────────────
        IReadOnlyList<ISqlExpression> qualifies = BuildQualifyExpressions(outputSink);

        // ── ORDER BY ──────────────────────────────────────────────────────────
        IReadOnlyList<OrderBinding> orderBindings = ResolveOrderBindings(outputSink);
        var orders = orderBindings.Select(b => (Resolve(b.NodeId, b.PinName), b.Descending)).ToList();

        // ── GROUP BY ──────────────────────────────────────────────────────────
        IReadOnlyList<GroupByBinding> groupByBindings = ResolveGroupByBindings(outputSink);
        var groups = groupByBindings.Select(b => Resolve(b.NodeId, b.PinName)).ToList();

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

    private IReadOnlyList<SelectBinding> BuildSelectBindings(NodeInstance? outputSink)
    {
        if (outputSink is null)
            return _allowLegacyBindings ? _graph.SelectOutputs : [];

        ValidateOutputSinkProjectionContract(outputSink);

        var bindings = new List<SelectBinding>();
        var visitedContainerPins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Connection connection in _graph.Connections.Where(c =>
                     c.ToNodeId == outputSink.Id
                     && (c.ToPinName.Equals("column", StringComparison.OrdinalIgnoreCase)
                         || c.ToPinName.Equals("columns", StringComparison.OrdinalIgnoreCase))))
        {
            AppendSelectBinding(connection.FromNodeId, connection.FromPinName, bindings, visitedContainerPins);
        }

        if (bindings.Count > 0)
            return bindings;

        return _allowLegacyBindings ? _graph.SelectOutputs : [];
    }

    private void AppendSelectBinding(
        string sourceNodeId,
        string sourcePinName,
        ICollection<SelectBinding> bindings,
        ISet<string> visitedContainerPins)
    {
        if (IsProjectionContainerPin(sourceNodeId, sourcePinName))
        {
            string key = $"{sourceNodeId}::{sourcePinName}";
            if (!visitedContainerPins.Add(key))
                return;

            foreach (Connection nested in _graph.Connections.Where(c =>
                         c.ToNodeId == sourceNodeId && IsProjectionContainerInputPin(sourceNodeId, c.ToPinName)))
            {
                AppendSelectBinding(nested.FromNodeId, nested.FromPinName, bindings, visitedContainerPins);
            }

            return;
        }

        bindings.Add(new SelectBinding(sourceNodeId, sourcePinName));
    }

    private bool IsProjectionContainerPin(string nodeId, string pinName)
    {
        if (!pinName.Equals("result", StringComparison.OrdinalIgnoreCase))
            return false;

        return _graph.NodeMap.TryGetValue(nodeId, out NodeInstance? node)
            && node.Type is NodeType.ColumnList or NodeType.ColumnSetBuilder or NodeType.ColumnSetMerge;
    }

    private bool IsProjectionContainerInputPin(string nodeId, string pinName)
    {
        if (!_graph.NodeMap.TryGetValue(nodeId, out NodeInstance? node))
            return false;

        if (node.Type == NodeType.ColumnSetMerge)
            return pinName.Equals("sets", StringComparison.OrdinalIgnoreCase);

        return pinName.Equals("columns", StringComparison.OrdinalIgnoreCase)
            || pinName.Equals("metadata", StringComparison.OrdinalIgnoreCase);
    }

    private NodeInstance? ResolvePrimaryOutputSink()
    {
        List<NodeInstance> candidates = _graph.Nodes
            .Where(n => n.Type is NodeType.ResultOutput or NodeType.SelectOutput)
            .Where(n => !IsCteSubgraphOutputNode(n.Id))
            .ToList();

        if (candidates.Count == 0)
            return null;
        if (candidates.Count == 1)
            return candidates[0];

        throw new InvalidOperationException(
            "Multiple ResultOutput/SelectOutput nodes detected in the same graph. "
                + "Connect exactly one output sink for SQL generation."
        );
    }

    private bool IsCteSubgraphOutputNode(string nodeId)
    {
        return _graph.Connections.Any(c =>
            c.FromNodeId == nodeId
            && c.FromPinName.Equals("result", StringComparison.OrdinalIgnoreCase)
            && c.ToPinName.Equals("query", StringComparison.OrdinalIgnoreCase)
            && _graph.NodeMap.TryGetValue(c.ToNodeId, out NodeInstance? target)
            && target.Type == NodeType.CteDefinition
        );
    }

    private void ValidateOutputSinkProjectionContract(NodeInstance outputSink)
    {
        bool hasProjectionWire = _graph.Connections.Any(c =>
            c.ToNodeId == outputSink.Id
            && (
                c.ToPinName.Equals("column", StringComparison.OrdinalIgnoreCase)
                || c.ToPinName.Equals("columns", StringComparison.OrdinalIgnoreCase)
            )
        );

        if (hasProjectionWire || (_allowLegacyBindings && _graph.SelectOutputs.Count > 0))
            return;

        throw new InvalidOperationException(
            $"Output sink '{outputSink.Id}' has no connected projection source. "
                + "Connect at least one column/columnset to ResultOutput."
        );
    }

    private IReadOnlyList<WhereBinding> ResolveWhereBindings(NodeInstance? outputSink)
    {
        if (outputSink is null)
            return _allowLegacyBindings ? _graph.WhereConditions : [];

        List<WhereBinding> bindings = _graph.Connections
            .Where(c => c.ToNodeId == outputSink.Id && c.ToPinName.Equals("where", StringComparison.OrdinalIgnoreCase))
            .Select(c => new WhereBinding(c.FromNodeId, c.FromPinName))
            .ToList();

        if (bindings.Count > 0)
            return bindings;

        return _allowLegacyBindings ? _graph.WhereConditions : [];
    }

    private IReadOnlyList<HavingBinding> ResolveHavingBindings(NodeInstance? outputSink)
    {
        if (outputSink is null)
            return _allowLegacyBindings ? _graph.Havings : [];

        List<HavingBinding> bindings = _graph.Connections
            .Where(c => c.ToNodeId == outputSink.Id && c.ToPinName.Equals("having", StringComparison.OrdinalIgnoreCase))
            .Select(c => new HavingBinding(c.FromNodeId, c.FromPinName))
            .ToList();

        if (bindings.Count > 0)
            return bindings;

        return _allowLegacyBindings ? _graph.Havings : [];
    }

    private IReadOnlyList<QualifyBinding> ResolveQualifyBindings(NodeInstance? outputSink)
    {
        if (outputSink is null)
            return _allowLegacyBindings ? _graph.Qualifies : [];

        List<QualifyBinding> bindings = _graph.Connections
            .Where(c => c.ToNodeId == outputSink.Id && c.ToPinName.Equals("qualify", StringComparison.OrdinalIgnoreCase))
            .Select(c => new QualifyBinding(c.FromNodeId, c.FromPinName))
            .ToList();

        if (bindings.Count > 0)
            return bindings;

        return _allowLegacyBindings ? _graph.Qualifies : [];
    }

    private IReadOnlyList<OrderBinding> ResolveOrderBindings(NodeInstance? outputSink)
    {
        if (outputSink is null)
            return _allowLegacyBindings ? _graph.OrderBys : [];

        List<OrderBinding> ascending = _graph.Connections
            .Where(c => c.ToNodeId == outputSink.Id && c.ToPinName.Equals("order_by", StringComparison.OrdinalIgnoreCase))
            .Select(c => new OrderBinding(c.FromNodeId, c.FromPinName, Descending: false))
            .ToList();

        List<OrderBinding> descending = _graph.Connections
            .Where(c => c.ToNodeId == outputSink.Id && c.ToPinName.Equals("order_by_desc", StringComparison.OrdinalIgnoreCase))
            .Select(c => new OrderBinding(c.FromNodeId, c.FromPinName, Descending: true))
            .ToList();

        if (ascending.Count == 0 && descending.Count == 0)
            return _allowLegacyBindings ? _graph.OrderBys : [];

        ascending.AddRange(descending);
        return ascending;
    }

    private IReadOnlyList<GroupByBinding> ResolveGroupByBindings(NodeInstance? outputSink)
    {
        if (outputSink is null)
            return _allowLegacyBindings ? _graph.GroupBys : [];

        List<GroupByBinding> bindings = _graph.Connections
            .Where(c => c.ToNodeId == outputSink.Id && c.ToPinName.Equals("group_by", StringComparison.OrdinalIgnoreCase))
            .Select(c => new GroupByBinding(c.FromNodeId, c.FromPinName))
            .ToList();

        if (bindings.Count > 0)
            return bindings;

        return _allowLegacyBindings ? _graph.GroupBys : [];
    }

    private IReadOnlyList<ISqlExpression> BuildWhereExpressions(NodeInstance? outputSink)
    {
        IReadOnlyList<WhereBinding> whereBindings = ResolveWhereBindings(outputSink);
        if (whereBindings.Count == 0)
            return [];
        if (whereBindings.Count == 1)
            return [Resolve(whereBindings[0].NodeId, whereBindings[0].PinName)];

        // Build a tree that respects the LogicOp of each binding
        var result = new List<ISqlExpression>();
        foreach (WhereBinding binding in whereBindings)
        {
            ISqlExpression expr = Resolve(binding.NodeId, binding.PinName);
            result.Add(expr);
        }

        return result;
    }

    private IReadOnlyList<ISqlExpression> BuildHavingExpressions(NodeInstance? outputSink)
    {
        IReadOnlyList<HavingBinding> havingBindings = ResolveHavingBindings(outputSink);
        if (havingBindings.Count == 0)
            return [];

        return havingBindings.Select(b => Resolve(b.NodeId, b.PinName)).ToList();
    }

    private IReadOnlyList<ISqlExpression> BuildQualifyExpressions(NodeInstance? outputSink)
    {
        IReadOnlyList<QualifyBinding> qualifyBindings = ResolveQualifyBindings(outputSink);
        if (qualifyBindings.Count == 0)
            return [];

        return qualifyBindings.Select(b => Resolve(b.NodeId, b.PinName)).ToList();
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
        // Canonical model: single variadic "conditions" input pin.
        // Legacy compatibility: also accept cond_N from previously serialized graphs.
        var conditions = _graph
            .Connections.Where(c =>
                c.ToNodeId == node.Id
                && (
                    c.ToPinName.Equals("conditions", StringComparison.OrdinalIgnoreCase)
                    || c.ToPinName.StartsWith("cond_", StringComparison.OrdinalIgnoreCase)
                )
            )
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
