using DBWeaver.Expressions;
using DBWeaver.Nodes.Definitions;

namespace DBWeaver.Nodes.Compilers;

/// <summary>
/// Compiles aggregate function nodes: COUNT, SUM, AVG, MIN, MAX.
/// These nodes compute single values from multiple rows in a result set.
/// </summary>
public sealed class AggregateCompiler : INodeCompiler
{
    public bool CanCompile(NodeType nodeType) =>
        nodeType
            is NodeType.CountStar
                or NodeType.Sum
                or NodeType.Avg
                or NodeType.Min
                or NodeType.Max
                or NodeType.StringAgg
                or NodeType.WindowFunction;

    public ISqlExpression Compile(NodeInstance node, INodeCompilationContext ctx, string pinName)
    {
        return node.Type switch
        {
            NodeType.CountStar => new AggregateExpr(AggregateFunction.Count, null),
            NodeType.Sum => new AggregateExpr(
                AggregateFunction.Sum,
                ctx.ResolveInput(node.Id, "value")
            ),
            NodeType.Avg => new AggregateExpr(
                AggregateFunction.Avg,
                ctx.ResolveInput(node.Id, "value")
            ),
            NodeType.Min => new AggregateExpr(
                AggregateFunction.Min,
                ctx.ResolveInput(node.Id, "value")
            ),
            NodeType.Max => new AggregateExpr(
                AggregateFunction.Max,
                ctx.ResolveInput(node.Id, "value")
            ),
            NodeType.StringAgg => CompileStringAgg(node, ctx),
            NodeType.WindowFunction => CompileWindowFunction(node, ctx),

            _ => throw new NotSupportedException($"Cannot compile {node.Type}"),
        };
    }

    private static ISqlExpression CompileStringAgg(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression value = ctx.ResolveInput(node.Id, "value", PinDataType.Text);

        bool distinct =
            node.Parameters.TryGetValue("distinct", out string? d)
            && bool.TryParse(d, out bool isDistinct)
            && isDistinct;

        string separator = node.Parameters.TryGetValue("separator", out string? s) ? s ?? ", " : ", ";
        string sepSql = EmitContext.QuoteLiteral(separator);

        Connection? orderWire = ctx.Graph.GetSingleInputConnection(node.Id, "order_by");
        ISqlExpression? orderBy = orderWire is null
            ? null
            : ctx.Resolve(orderWire.FromNodeId, orderWire.FromPinName);

        string valueSql = value.Emit(ctx.EmitContext);
        string distinctPrefix = distinct ? "DISTINCT " : string.Empty;

        string sql = ctx.EmitContext.Provider switch
        {
            Core.DatabaseProvider.SqlServer => orderBy is null
                ? $"STRING_AGG({distinctPrefix}{valueSql}, {sepSql})"
                : $"STRING_AGG({distinctPrefix}{valueSql}, {sepSql}) WITHIN GROUP (ORDER BY {orderBy.Emit(ctx.EmitContext)})",

            Core.DatabaseProvider.MySql =>
                orderBy is null
                    ? $"GROUP_CONCAT({distinctPrefix}{valueSql} SEPARATOR {sepSql})"
                    : $"GROUP_CONCAT({distinctPrefix}{valueSql} ORDER BY {orderBy.Emit(ctx.EmitContext)} SEPARATOR {sepSql})",

            Core.DatabaseProvider.Postgres => orderBy is null
                ? $"STRING_AGG({distinctPrefix}{valueSql}, {sepSql})"
                : $"STRING_AGG({distinctPrefix}{valueSql}, {sepSql} ORDER BY {orderBy.Emit(ctx.EmitContext)})",

            _ => $"STRING_AGG({distinctPrefix}{valueSql}, {sepSql})",
        };

        return new RawSqlExpr(sql, PinDataType.Text);
    }

    private static ISqlExpression CompileWindowFunction(NodeInstance node, INodeCompilationContext ctx)
    {
        string function = node.Parameters.TryGetValue("function", out string? fn)
            ? fn ?? "RowNumber"
            : "RowNumber";
        WindowFunctionKind kind = ParseWindowFunctionKind(function);
        IReadOnlyList<ISqlExpression> partitionBy = ResolveWindowPartitionParts(node, ctx);
        IReadOnlyList<WindowOrderByExpr> orderBy = ResolveWindowOrderParts(node, ctx);
        WindowFrameSpec frame = ParseWindowFrame(node);
        (WindowFrameBound? customStart, WindowFrameBound? customEnd) = ParseCustomWindowFrameBounds(node, frame);

        var over = new WindowOverExpr(
            partitionBy,
            orderBy,
            frame,
            customStart,
            customEnd,
            ForceSqlServerDummyOrderBy: true
        );

        ISqlExpression? valueExpr = ResolveOptionalInput(node, ctx, "value");
        int? offset = ResolvePositiveIntParam(node, "offset", defaultValue: 1);
        ISqlExpression? defaultExpr = ResolveWindowDefaultExpr(node, ctx);
        int? ntileGroups = ResolvePositiveIntParam(node, "ntile_groups", defaultValue: 4);

        return new WindowFunctionExpr(kind, over, valueExpr, offset, defaultExpr, ntileGroups);
    }

    private static ISqlExpression? ResolveWindowDefaultExpr(NodeInstance node, INodeCompilationContext ctx)
    {
        Connection? defaultWire = ctx.Graph.GetSingleInputConnection(node.Id, "default");
        if (defaultWire is not null)
            return ctx.Resolve(defaultWire.FromNodeId, defaultWire.FromPinName);

        if (!node.Parameters.TryGetValue("default_value", out string? raw) || string.IsNullOrWhiteSpace(raw))
            return null;

        string trimmed = raw.Trim();
        if (trimmed.Equals("null", StringComparison.OrdinalIgnoreCase))
            return NullExpr.Instance;

        if (
            double.TryParse(
                trimmed,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double numeric
            )
        )
            return new NumberLiteralExpr(numeric);

        if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase))
            return new LiteralExpr("TRUE", PinDataType.Boolean);
        if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase))
            return new LiteralExpr("FALSE", PinDataType.Boolean);

        return new StringLiteralExpr(trimmed);
    }

    private static WindowFunctionKind ParseWindowFunctionKind(string function)
    {
        return function.Trim().ToLowerInvariant() switch
        {
            "rownumber" => WindowFunctionKind.RowNumber,
            "rank" => WindowFunctionKind.Rank,
            "denserank" => WindowFunctionKind.DenseRank,
            "ntile" => WindowFunctionKind.Ntile,
            "lag" => WindowFunctionKind.Lag,
            "lead" => WindowFunctionKind.Lead,
            "firstvalue" => WindowFunctionKind.FirstValue,
            "lastvalue" => WindowFunctionKind.LastValue,
            "sumover" => WindowFunctionKind.SumOver,
            "avgover" => WindowFunctionKind.AvgOver,
            "minover" => WindowFunctionKind.MinOver,
            "maxover" => WindowFunctionKind.MaxOver,
            "countover" => WindowFunctionKind.CountOver,
            _ => throw new NotSupportedException(
                $"Window function '{function}' is not supported. Supported values: RowNumber, Rank, DenseRank, Ntile, Lag, Lead, FirstValue, LastValue, SumOver, AvgOver, MinOver, MaxOver, CountOver."
            ),
        };
    }

    private static WindowFrameSpec ParseWindowFrame(NodeInstance node)
    {
        string frame = node.Parameters.TryGetValue("frame", out string? f)
            ? f ?? "None"
            : "None";

        return frame.Trim() switch
        {
            "UnboundedPreceding_CurrentRow" => WindowFrameSpec.RowsUnboundedPrecedingToCurrentRow,
            "CurrentRow_UnboundedFollowing" => WindowFrameSpec.RowsCurrentRowToUnboundedFollowing,
            "Custom" => WindowFrameSpec.RowsCustomBetween,
            _ => WindowFrameSpec.None,
        };
    }

    private static (WindowFrameBound? Start, WindowFrameBound? End) ParseCustomWindowFrameBounds(
        NodeInstance node,
        WindowFrameSpec frame)
    {
        if (frame != WindowFrameSpec.RowsCustomBetween)
            return (null, null);

        string startRaw = node.Parameters.TryGetValue("frame_start", out string? s)
            ? s ?? "UnboundedPreceding"
            : "UnboundedPreceding";
        string endRaw = node.Parameters.TryGetValue("frame_end", out string? e)
            ? e ?? "CurrentRow"
            : "CurrentRow";

        int? startOffset = ResolvePositiveIntParam(node, "frame_start_offset", defaultValue: 1);
        int? endOffset = ResolvePositiveIntParam(node, "frame_end_offset", defaultValue: 1);

        WindowFrameBound start = new(ParseWindowFrameBoundKind(startRaw), startOffset);
        WindowFrameBound end = new(ParseWindowFrameBoundKind(endRaw), endOffset);
        return (start, end);
    }

    private static WindowFrameBoundKind ParseWindowFrameBoundKind(string raw)
    {
        return raw.Trim() switch
        {
            "UnboundedPreceding" => WindowFrameBoundKind.UnboundedPreceding,
            "Preceding" => WindowFrameBoundKind.Preceding,
            "CurrentRow" => WindowFrameBoundKind.CurrentRow,
            "Following" => WindowFrameBoundKind.Following,
            "UnboundedFollowing" => WindowFrameBoundKind.UnboundedFollowing,
            _ => WindowFrameBoundKind.CurrentRow,
        };
    }

    private static List<ISqlExpression> ResolveWindowPartitionParts(NodeInstance node, INodeCompilationContext ctx)
    {
        return ctx
            .Graph.Connections.Where(c => c.ToNodeId == node.Id)
            .Where(c => c.ToPinName.StartsWith("partition_", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => ExtractPinOrder(c.ToPinName))
            .Select(c => ctx.Resolve(c.FromNodeId, c.FromPinName))
            .ToList();
    }

    private static List<WindowOrderByExpr> ResolveWindowOrderParts(NodeInstance node, INodeCompilationContext ctx)
    {
        return ctx
            .Graph.Connections.Where(c => c.ToNodeId == node.Id)
            .Where(c => c.ToPinName.StartsWith("order_", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => ExtractPinOrder(c.ToPinName))
            .Select(c => new WindowOrderByExpr(
                ctx.Resolve(c.FromNodeId, c.FromPinName),
                ResolveOrderDescending(node, c.ToPinName)
            ))
            .ToList();
    }

    private static ISqlExpression? ResolveOptionalInput(
        NodeInstance node,
        INodeCompilationContext ctx,
        string pinName
    )
    {
        Connection? wire = ctx.Graph.GetSingleInputConnection(node.Id, pinName);
        if (wire is null)
            return null;

        return ctx.Resolve(wire.FromNodeId, wire.FromPinName);
    }

    private static int ResolvePositiveIntParam(NodeInstance node, string name, int defaultValue)
    {
        return node.Parameters.TryGetValue(name, out string? raw)
            && int.TryParse(raw, out int parsed)
            && parsed > 0
            ? parsed
            : defaultValue;
    }

    private static bool ResolveOrderDescending(NodeInstance node, string pinName)
    {
        int pinOrder = ExtractPinOrder(pinName);
        string paramName = $"order_{pinOrder}_desc";
        return node.Parameters.TryGetValue(paramName, out string? d)
            && bool.TryParse(d, out bool parsed)
            && parsed;
    }

    private static int ExtractPinOrder(string pinName)
    {
        int idx = pinName.LastIndexOf('_');
        if (idx < 0 || idx == pinName.Length - 1)
            return int.MaxValue;

        return int.TryParse(pinName[(idx + 1)..], out int n) ? n : int.MaxValue;
    }
}
