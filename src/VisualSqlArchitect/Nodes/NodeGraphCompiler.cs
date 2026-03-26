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
public sealed class NodeGraphCompiler
{
    private readonly EmitContext _ctx;
    private readonly NodeGraph   _graph;

    // Memoisation: nodeId → compiled expression
    private readonly Dictionary<string, ISqlExpression> _cache = new();

    public NodeGraphCompiler(NodeGraph graph, EmitContext ctx)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        _ctx   = ctx   ?? throw new ArgumentNullException(nameof(ctx));
    }

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Compiles the entire graph and returns a <see cref="CompiledNodeGraph"/>
    /// ready for the <see cref="QueryEngine.QueryGeneratorService"/>.
    /// </summary>
    public CompiledNodeGraph Compile()
    {
        // Warm the memo cache in topological order (no redundant recomputes)
        foreach (var node in _graph.TopologicalOrder())
            Resolve(node.Id, "result");   // "result" is the default output pin name

        // ── SELECT expressions ────────────────────────────────────────────────
        var selects = _graph.SelectOutputs
            .Select(b => (Resolve(b.NodeId, b.PinName), b.Alias ?? GetNodeAlias(b.NodeId)))
            .ToList();

        // ── WHERE expressions ─────────────────────────────────────────────────
        var wheres = BuildWhereExpressions();

        // ── ORDER BY ──────────────────────────────────────────────────────────
        var orders = _graph.OrderBys
            .Select(b => (Resolve(b.NodeId, b.PinName), b.Descending))
            .ToList();

        // ── GROUP BY ──────────────────────────────────────────────────────────
        var groups = _graph.GroupBys
            .Select(b => Resolve(b.NodeId, b.PinName))
            .ToList();

        return new CompiledNodeGraph(selects, wheres, orders, groups,
            _graph.Limit, _graph.Offset);
    }

    // ── Node resolution (memo + dispatch) ────────────────────────────────────

    /// <summary>
    /// Returns the compiled expression for <paramref name="nodeId"/>'s named pin.
    /// Results are memoised per node (output pin is implicit for single-output nodes).
    /// </summary>
    private ISqlExpression Resolve(string nodeId, string pinName = "result")
    {
        var cacheKey = $"{nodeId}::{pinName}";
        if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

        var node = _graph.NodeMap[nodeId];
        var expr = CompileNode(node, pinName);

        _cache[cacheKey] = expr;
        return expr;
    }

    /// <summary>Resolves an input pin: wire → upstream node, or literal fallback.</summary>
    private ISqlExpression ResolveInput(string nodeId, string pinName,
        PinDataType expectedType = PinDataType.Any)
    {
        var wire = _graph.GetSingleInputConnection(nodeId, pinName);
        if (wire is not null)
            return Resolve(wire.FromNodeId, wire.FromPinName);

        // No wire — check for a literal value in the node's PinLiterals dict
        var node = _graph.NodeMap[nodeId];
        if (node.PinLiterals.TryGetValue(pinName, out var literal))
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
        var wires = _graph.GetInputConnections(nodeId, pinName);
        return wires
            .Select(w => Resolve(w.FromNodeId, w.FromPinName))
            .ToList();
    }

    // ── Node dispatch ─────────────────────────────────────────────────────────

    private ISqlExpression CompileNode(NodeInstance node, string pinName)
    {
        return node.Type switch
        {
            // ── Data Source ───────────────────────────────────────────────────
            NodeType.TableSource   => CompileTableSourcePin(node, pinName),
            NodeType.Alias         => CompileAlias(node),

            // ── String ────────────────────────────────────────────────────────
            NodeType.Upper         => new FunctionCallExpr(SqlFn.Upper,
                                         new[] { ResolveInput(node.Id, "text") },
                                         PinDataType.Text),

            NodeType.Lower         => new FunctionCallExpr(SqlFn.Lower,
                                         new[] { ResolveInput(node.Id, "text") },
                                         PinDataType.Text),

            NodeType.Trim          => new FunctionCallExpr(SqlFn.Trim,
                                         new[] { ResolveInput(node.Id, "text") },
                                         PinDataType.Text),

            NodeType.StringLength  => new FunctionCallExpr(SqlFn.Length,
                                         new[] { ResolveInput(node.Id, "text") },
                                         PinDataType.Number),

            NodeType.Concat        => CompileConcat(node),
            NodeType.Substring     => CompileSubstring(node),
            NodeType.RegexMatch    => CompileRegexMatch(node),
            NodeType.RegexReplace  => CompileRegexReplace(node),
            NodeType.RegexExtract  => CompileRegexExtract(node),
            NodeType.Replace       => CompileReplace(node),

            // ── Math ──────────────────────────────────────────────────────────
            NodeType.Round         => CompileRound(node),
            NodeType.Abs           => CompileSimpleMath(node, "ABS"),
            NodeType.Ceil          => CompileCeilFloor(node, ceil: true),
            NodeType.Floor         => CompileCeilFloor(node, ceil: false),
            NodeType.Add           => CompileArithmetic(node, "+"),
            NodeType.Subtract      => CompileArithmetic(node, "-"),
            NodeType.Multiply      => CompileArithmetic(node, "*"),
            NodeType.Divide        => CompileArithmetic(node, "/"),

            // ── Aggregates ────────────────────────────────────────────────────
            NodeType.CountStar     => new AggregateExpr(AggregateFunction.Count, null),
            NodeType.Sum           => new AggregateExpr(AggregateFunction.Sum,
                                         ResolveInput(node.Id, "value")),
            NodeType.Avg           => new AggregateExpr(AggregateFunction.Avg,
                                         ResolveInput(node.Id, "value")),
            NodeType.Min           => new AggregateExpr(AggregateFunction.Min,
                                         ResolveInput(node.Id, "value")),
            NodeType.Max           => new AggregateExpr(AggregateFunction.Max,
                                         ResolveInput(node.Id, "value")),

            // ── Cast ──────────────────────────────────────────────────────────
            NodeType.Cast          => CompileCast(node),

            // ── Comparisons ───────────────────────────────────────────────────
            NodeType.Equals        => CompileComparison(node, ComparisonOperator.Eq),
            NodeType.NotEquals     => CompileComparison(node, ComparisonOperator.Neq),
            NodeType.GreaterThan   => CompileComparison(node, ComparisonOperator.Gt),
            NodeType.GreaterOrEqual=> CompileComparison(node, ComparisonOperator.Gte),
            NodeType.LessThan      => CompileComparison(node, ComparisonOperator.Lt),
            NodeType.LessOrEqual   => CompileComparison(node, ComparisonOperator.Lte),
            NodeType.Between       => CompileBetween(node, negate: false),
            NodeType.NotBetween    => CompileBetween(node, negate: true),
            NodeType.IsNull        => new IsNullExpr(ResolveInput(node.Id, "value")),
            NodeType.IsNotNull     => new IsNullExpr(ResolveInput(node.Id, "value"), Negate: true),
            NodeType.Like          => CompileLike(node, negate: false),
            NodeType.NotLike       => CompileLike(node, negate: true),

            // ── Logic gates ───────────────────────────────────────────────────
            NodeType.And           => new LogicGateExpr(LogicOperator.And,
                                         ResolveMultiInput(node.Id, "conditions")),
            NodeType.Or            => new LogicGateExpr(LogicOperator.Or,
                                         ResolveMultiInput(node.Id, "conditions")),
            NodeType.Not           => new NotExpr(ResolveInput(node.Id, "condition")),

            // ── JSON ─────────────────────────────────────────────────────────
            NodeType.JsonExtract
                or NodeType.JsonValue => CompileJsonExtract(node),
            NodeType.JsonArrayLength  => CompileJsonArrayLength(node),

            // ── Value Transform ───────────────────────────────────────────────
            NodeType.NullFill  => CompileNullFill(node),
            NodeType.EmptyFill => CompileEmptyFill(node),
            NodeType.ValueMap  => CompileValueMap(node),

            _ => throw new NotSupportedException(
                $"NodeType.{node.Type} has no compiler implementation.")
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INDIVIDUAL NODE COMPILERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A TableSource node has one output pin per column.
    /// The pin name IS the column name; the table alias comes from TableFullName.
    /// </summary>
    private ISqlExpression CompileTableSourcePin(NodeInstance node, string pinName)
    {
        if (node.TableFullName is null)
            throw new InvalidOperationException(
                $"TableSource node '{node.Id}' has no TableFullName.");

        // Use the last segment after '.' as the alias; full name stays in FROM
        var parts = node.TableFullName.Split('.');
        var alias = parts.Last();
        return new ColumnExpr(alias, pinName);
    }

    private ISqlExpression CompileAlias(NodeInstance node)
    {
        var inner = ResolveInput(node.Id, "expression");
        var aliasName = node.Parameters.TryGetValue("alias", out var a) && !string.IsNullOrWhiteSpace(a)
            ? a.Trim()
            : "alias";
        return new AliasExpr(inner, aliasName);
    }

    private ISqlExpression CompileConcat(NodeInstance node)
    {
        var a = ResolveInput(node.Id, "a");
        var b = ResolveInput(node.Id, "b");
        var sep = _graph.NodeMap[node.Id].PinLiterals.TryGetValue("separator", out var s) && !string.IsNullOrEmpty(s)
            ? new StringLiteralExpr(s) as ISqlExpression
            : null;

        if (sep is not null)
            return new FunctionCallExpr(SqlFn.Concat, new[] { a, sep, b }, PinDataType.Text);

        return new FunctionCallExpr(SqlFn.Concat, new[] { a, b }, PinDataType.Text);
    }

    private ISqlExpression CompileSubstring(NodeInstance node)
    {
        var text = ResolveInput(node.Id, "text");

        var startExpr  = ResolveInputOrParam(node, "start",  "1",   PinDataType.Number);
        var lengthExpr = ResolveInputOrParam(node, "length", null,  PinDataType.Number);

        // Provider-specific SUBSTRING dialect
        return _ctx.Provider switch
        {
            // MySQL: SUBSTRING(str, start, len) — len is optional but changes behaviour
            Core.DatabaseProvider.MySql when lengthExpr is not NullExpr =>
                new RawSqlExpr(
                    $"SUBSTRING({text.Emit(_ctx)}, {startExpr.Emit(_ctx)}, {lengthExpr.Emit(_ctx)})",
                    PinDataType.Text),

            Core.DatabaseProvider.MySql =>
                new RawSqlExpr($"SUBSTRING({text.Emit(_ctx)}, {startExpr.Emit(_ctx)})", PinDataType.Text),

            // SQL Server: SUBSTRING(str, start, len) — LEN() required
            Core.DatabaseProvider.SqlServer =>
                new RawSqlExpr(
                    $"SUBSTRING({text.Emit(_ctx)}, {startExpr.Emit(_ctx)}, " +
                    $"{(lengthExpr is NullExpr ? $"LEN({text.Emit(_ctx)})" : lengthExpr.Emit(_ctx))})",
                    PinDataType.Text),

            // Postgres: SUBSTRING(str FROM start FOR len) — standard SQL syntax
            _ when lengthExpr is not NullExpr =>
                new RawSqlExpr(
                    $"SUBSTRING({text.Emit(_ctx)} FROM {startExpr.Emit(_ctx)} FOR {lengthExpr.Emit(_ctx)})",
                    PinDataType.Text),

            _ =>
                new RawSqlExpr(
                    $"SUBSTRING({text.Emit(_ctx)} FROM {startExpr.Emit(_ctx)})",
                    PinDataType.Text),
        };
    }

    private ISqlExpression CompileRegexMatch(NodeInstance node)
    {
        var text    = ResolveInput(node.Id, "text");
        var pattern = node.Parameters.TryGetValue("pattern", out var p)
            ? $"'{p}'"
            : "''";

        return new FunctionCallExpr(SqlFn.Regex, new[] { text }, PinDataType.Boolean)
        {
            // Hack: FunctionCallExpr passes args to registry.GetFunction —
            // we need to pass both args. Use RawSql to compile the registry call here.
        } is var _ ? new RawSqlExpr(
            _ctx.Registry.GetFunction(SqlFn.Regex, text.Emit(_ctx), pattern),
            PinDataType.Boolean) : NullExpr.Instance;
    }

    private ISqlExpression CompileRegexReplace(NodeInstance node)
    {
        var text        = ResolveInput(node.Id, "text");
        var pattern     = node.Parameters.TryGetValue("pattern",     out var p) ? $"'{p.Replace("'", "''")}'" : "''";
        var replacement = node.Parameters.TryGetValue("replacement", out var r) ? $"'{r.Replace("'", "''")}'" : "''";
        return new RawSqlExpr(
            _ctx.Registry.GetFunction(SqlFn.RegexReplace, text.Emit(_ctx), pattern, replacement),
            PinDataType.Text);
    }

    private ISqlExpression CompileRegexExtract(NodeInstance node)
    {
        var text    = ResolveInput(node.Id, "text");
        var pattern = node.Parameters.TryGetValue("pattern", out var p) ? $"'{p.Replace("'", "''")}'" : "''";
        return new RawSqlExpr(
            _ctx.Registry.GetFunction(SqlFn.RegexExtract, text.Emit(_ctx), pattern),
            PinDataType.Text);
    }

    private ISqlExpression CompileReplace(NodeInstance node)
    {
        var value  = ResolveInput(node.Id, "value");
        var search = node.Parameters.TryGetValue("search",      out var s) ? $"'{s.Replace("'", "''")}'" : "''";
        var repl   = node.Parameters.TryGetValue("replacement", out var r) ? $"'{r.Replace("'", "''")}'" : "''";
        return new RawSqlExpr(
            _ctx.Registry.GetFunction(SqlFn.Replace, value.Emit(_ctx), search, repl),
            PinDataType.Text);
    }

    private ISqlExpression CompileNullFill(NodeInstance node)
    {
        var value    = ResolveInput(node.Id, "value");
        var fallback = node.Parameters.TryGetValue("fallback", out var f) && !string.IsNullOrEmpty(f)
            ? $"'{f.Replace("'", "''")}'"
            : "NULL";
        return new RawSqlExpr(
            _ctx.Registry.GetFunction(SqlFn.Coalesce, value.Emit(_ctx), fallback),
            PinDataType.Any);
    }

    private ISqlExpression CompileEmptyFill(NodeInstance node)
    {
        var value    = ResolveInput(node.Id, "value");
        var fallback = node.Parameters.TryGetValue("fallback", out var f) && !string.IsNullOrEmpty(f)
            ? $"'{f.Replace("'", "''")}'"
            : "NULL";
        // COALESCE(NULLIF(TRIM(value), ''), fallback)
        var trimmed = _ctx.Registry.GetFunction(SqlFn.Trim,  value.Emit(_ctx));
        var nulled  = _ctx.Registry.GetFunction(SqlFn.NullIf, trimmed, "''");
        return new RawSqlExpr(
            _ctx.Registry.GetFunction(SqlFn.Coalesce, nulled, fallback),
            PinDataType.Text);
    }

    private ISqlExpression CompileValueMap(NodeInstance node)
    {
        var value = ResolveInput(node.Id, "value");
        var src   = node.Parameters.TryGetValue("src", out var s) ? $"'{s.Replace("'", "''")}'" : "''";
        var dst   = node.Parameters.TryGetValue("dst", out var d) ? $"'{d.Replace("'", "''")}'" : "NULL";
        var val   = value.Emit(_ctx);
        return new RawSqlExpr(
            $"CASE WHEN ({val}) = {src} THEN {dst} ELSE ({val}) END",
            PinDataType.Any);
    }

    private ISqlExpression CompileRound(NodeInstance node)
    {
        var value = ResolveInput(node.Id, "value", PinDataType.Number);
        var prec  = ResolveInputOrParam(node, "precision", "0", PinDataType.Number);
        return new RawSqlExpr($"ROUND({value.Emit(_ctx)}, {prec.Emit(_ctx)})", PinDataType.Number);
    }

    private ISqlExpression CompileSimpleMath(NodeInstance node, string fn) =>
        new RawSqlExpr($"{fn}({ResolveInput(node.Id, "value", PinDataType.Number).Emit(_ctx)})",
            PinDataType.Number);

    private ISqlExpression CompileCeilFloor(NodeInstance node, bool ceil)
    {
        var value = ResolveInput(node.Id, "value", PinDataType.Number).Emit(_ctx);
        // SQL Server: CEILING; Postgres/MySQL: CEIL and FLOOR are standard
        var fn = ceil
            ? (_ctx.Provider == Core.DatabaseProvider.SqlServer ? "CEILING" : "CEIL")
            : "FLOOR";
        return new RawSqlExpr($"{fn}({value})", PinDataType.Number);
    }

    private ISqlExpression CompileArithmetic(NodeInstance node, string op)
    {
        var a = ResolveInput(node.Id, "a", PinDataType.Number).Emit(_ctx);
        var b = ResolveInput(node.Id, "b", PinDataType.Number).Emit(_ctx);
        return new RawSqlExpr($"({a} {op} {b})", PinDataType.Number);
    }

    private ISqlExpression CompileCast(NodeInstance node)
    {
        var value = ResolveInput(node.Id, "value");
        var typeName = node.Parameters.TryGetValue("targetType", out var t) ? t : "Text";
        var targetType = Enum.Parse<CastTargetType>(typeName, ignoreCase: true);
        return new CastExpr(value, targetType);
    }

    private ISqlExpression CompileComparison(NodeInstance node, ComparisonOperator op)
    {
        var left  = ResolveInput(node.Id, "left");
        var right = ResolveInput(node.Id, "right");
        return new ComparisonExpr(left, op, right);
    }

    private ISqlExpression CompileBetween(NodeInstance node, bool negate)
    {
        var value = ResolveInput(node.Id, "value");
        var lo    = ResolveInput(node.Id, "low");
        var hi    = ResolveInput(node.Id, "high");
        return new BetweenExpr(value, lo, hi, negate);
    }

    private ISqlExpression CompileLike(NodeInstance node, bool negate)
    {
        var text    = ResolveInput(node.Id, "text");
        var pattern = node.Parameters.TryGetValue("pattern", out var p)
            ? new StringLiteralExpr(p) as ISqlExpression
            : NullExpr.Instance;
        return new ComparisonExpr(text,
            negate ? ComparisonOperator.NotLike : ComparisonOperator.Like,
            pattern);
    }

    // ── JSON nodes ────────────────────────────────────────────────────────────

    private ISqlExpression CompileJsonExtract(NodeInstance node)
    {
        var json = ResolveInput(node.Id, "json", PinDataType.Json);
        var path = node.Parameters.TryGetValue("path", out var p) ? p : "$";

        // Translate output type hint
        var outputTypeStr = node.Parameters.TryGetValue("outputType", out var ot) ? ot : "Text";
        var outputType = outputTypeStr.ToLower() switch
        {
            "number"  => PinDataType.Number,
            "boolean" => PinDataType.Boolean,
            "json"    => PinDataType.Json,
            _         => PinDataType.Text
        };

        // Registry dispatches to the correct provider dialect
        return new FunctionCallExpr(
            SqlFn.JsonExtract,
            new[] { json, new StringLiteralExpr(path) as ISqlExpression },
            outputType);
    }

    private ISqlExpression CompileJsonArrayLength(NodeInstance node)
    {
        var json = ResolveInput(node.Id, "json", PinDataType.Json);
        var path = node.Parameters.TryGetValue("path", out var p) ? p : "$";

        return new FunctionCallExpr(
            SqlFn.JsonArrayLength,
            new[] { json, new StringLiteralExpr(path) as ISqlExpression },
            PinDataType.Number);
    }

    // ── WHERE combination ─────────────────────────────────────────────────────

    private IReadOnlyList<ISqlExpression> BuildWhereExpressions()
    {
        if (_graph.WhereConditions.Count == 0) return Array.Empty<ISqlExpression>();
        if (_graph.WhereConditions.Count == 1)
            return new[] { Resolve(_graph.WhereConditions[0].NodeId,
                                   _graph.WhereConditions[0].PinName) };

        // Build a tree that respects the LogicOp of each binding
        var result = new List<ISqlExpression>();
        foreach (var binding in _graph.WhereConditions)
        {
            var expr = Resolve(binding.NodeId, binding.PinName);
            result.Add(expr);
        }

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ISqlExpression ResolveInputOrParam(
        NodeInstance node, string pinName, string? defaultLiteral, PinDataType type)
    {
        var wire = _graph.GetSingleInputConnection(node.Id, pinName);
        if (wire is not null) return Resolve(wire.FromNodeId, wire.FromPinName);

        if (node.PinLiterals.TryGetValue(pinName, out var pinLit))
            return BuildLiteral(pinLit, type);

        if (node.Parameters.TryGetValue(pinName, out var param))
            return BuildLiteral(param, type);

        return defaultLiteral is null ? NullExpr.Instance : BuildLiteral(defaultLiteral, type);
    }

    private static ISqlExpression BuildLiteral(string raw, PinDataType type) =>
        type switch
        {
            PinDataType.Number when double.TryParse(raw,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) =>
                new NumberLiteralExpr(d),

            PinDataType.Text => new StringLiteralExpr(raw),

            _ => new LiteralExpr(raw, type)
        };

    private string? GetNodeAlias(string nodeId) =>
        _graph.NodeMap.TryGetValue(nodeId, out var node) ? node.Alias : null;
}
