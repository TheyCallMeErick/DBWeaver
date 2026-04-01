using VisualSqlArchitect.Expressions;
using VisualSqlArchitect.Nodes.Definitions;
using VisualSqlArchitect.Registry;

namespace VisualSqlArchitect.Nodes.Compilers;

/// <summary>
/// Compiles mathematical transformation nodes: ROUND, ABS, CEIL, FLOOR, and arithmetic operations.
/// These nodes transform numeric values using SQL math functions.
/// </summary>
public sealed class MathTransformCompiler : INodeCompiler
{
    public bool CanCompile(NodeType nodeType) =>
        nodeType
            is NodeType.Round
                or NodeType.Abs
                or NodeType.Ceil
                or NodeType.Floor
                or NodeType.Add
                or NodeType.Subtract
                or NodeType.Multiply
                or NodeType.Divide
                or NodeType.DateAdd
                or NodeType.DateDiff
                or NodeType.DatePart
                or NodeType.DateFormat;

    public ISqlExpression Compile(NodeInstance node, INodeCompilationContext ctx, string pinName)
    {
        return node.Type switch
        {
            NodeType.Round => CompileRound(node, ctx),
            NodeType.Abs => CompileSimpleMath(node, ctx, "ABS"),
            NodeType.Ceil => CompileCeilFloor(node, ctx, ceil: true),
            NodeType.Floor => CompileCeilFloor(node, ctx, ceil: false),
            NodeType.Add => CompileArithmetic(node, ctx, "+"),
            NodeType.Subtract => CompileArithmetic(node, ctx, "-"),
            NodeType.Multiply => CompileArithmetic(node, ctx, "*"),
            NodeType.Divide => CompileArithmetic(node, ctx, "/"),
            NodeType.DateAdd => CompileDateAdd(node, ctx),
            NodeType.DateDiff => CompileDateDiff(node, ctx),
            NodeType.DatePart => CompileDatePart(node, ctx),
            NodeType.DateFormat => CompileDateFormat(node, ctx),

            _ => throw new NotSupportedException($"Cannot compile {node.Type}"),
        };
    }

    private static ISqlExpression CompileRound(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression value = ctx.ResolveInput(node.Id, "value");
        NumberLiteralExpr decimals = node.Parameters.TryGetValue("decimals", out string? d)
            ? new NumberLiteralExpr(int.Parse(d ?? "0"))
            : new NumberLiteralExpr(0);

        return new FunctionCallExpr("ROUND", [value, decimals], PinDataType.Number);
    }

    private static ISqlExpression CompileSimpleMath(
        NodeInstance node,
        INodeCompilationContext ctx,
        string functionName
    )
    {
        ISqlExpression value = ctx.ResolveInput(node.Id, "value");
        return new FunctionCallExpr(functionName, [value], PinDataType.Number);
    }

    private static ISqlExpression CompileCeilFloor(
        NodeInstance node,
        INodeCompilationContext ctx,
        bool ceil
    )
    {
        ISqlExpression value = ctx.ResolveInput(node.Id, "value");
        string funcName = ceil ? "CEIL" : "FLOOR";
        return new FunctionCallExpr(funcName, [value], PinDataType.Number);
    }

    private static ISqlExpression CompileArithmetic(
        NodeInstance node,
        INodeCompilationContext ctx,
        string op
    )
    {
        ISqlExpression left = ResolveArithmeticInput(node, ctx, primaryPin: "a", legacyPin: "left");
        ISqlExpression right = ResolveArithmeticInput(node, ctx, primaryPin: "b", legacyPin: "right");

        // Use RawSqlExpr for infix operators to maintain precedence
        return new RawSqlExpr(
            $"({left.Emit(ctx.EmitContext)} {op} {right.Emit(ctx.EmitContext)})",
            PinDataType.Number
        );
    }

    private static ISqlExpression ResolveArithmeticInput(
        NodeInstance node,
        INodeCompilationContext ctx,
        string primaryPin,
        string legacyPin
    )
    {
        bool hasPrimaryWire = ctx.Graph.GetSingleInputConnection(node.Id, primaryPin) is not null;
        bool hasPrimaryLiteral = node.PinLiterals.ContainsKey(primaryPin);
        if (hasPrimaryWire || hasPrimaryLiteral)
            return ctx.ResolveInput(node.Id, primaryPin);

        return ctx.ResolveInput(node.Id, legacyPin);
    }

    private static ISqlExpression CompileDateAdd(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression dateExpr = ctx.ResolveInput(node.Id, "date", PinDataType.DateTime);
        ISqlExpression amountExpr = ResolveInputOrNumberParam(node, ctx, "amount", "1");
        string unit = NormalizeDateUnit(GetParam(node, "unit", "day"));

        string dateSql = dateExpr.Emit(ctx.EmitContext);
        string amountSql = amountExpr.Emit(ctx.EmitContext);

        string sql = ctx.EmitContext.Provider switch
        {
            Core.DatabaseProvider.SqlServer =>
                $"DATEADD({unit.ToUpperInvariant()}, {amountSql}, {dateSql})",

            Core.DatabaseProvider.MySql =>
                $"DATE_ADD({dateSql}, INTERVAL {amountSql} {unit.ToUpperInvariant()})",

            Core.DatabaseProvider.Postgres =>
                $"({dateSql} + ({amountSql}) * INTERVAL '1 {unit}')",

            Core.DatabaseProvider.SQLite =>
                $"DATETIME({dateSql}, ({amountSql}) || ' {unit}')",

            _ => $"({dateSql} + ({amountSql}) * INTERVAL '1 {unit}')",
        };

        return new RawSqlExpr(sql, PinDataType.DateTime);
    }

    private static ISqlExpression CompileDateDiff(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression startExpr = ctx.ResolveInput(node.Id, "start", PinDataType.DateTime);
        ISqlExpression endExpr = ctx.ResolveInput(node.Id, "end", PinDataType.DateTime);
        string unit = NormalizeDateUnit(GetParam(node, "unit", "day"));

        string startSql = startExpr.Emit(ctx.EmitContext);
        string endSql = endExpr.Emit(ctx.EmitContext);

        string sql = ctx.EmitContext.Provider switch
        {
            Core.DatabaseProvider.SqlServer =>
                $"DATEDIFF({unit.ToUpperInvariant()}, {startSql}, {endSql})",

            Core.DatabaseProvider.MySql =>
                $"TIMESTAMPDIFF({unit.ToUpperInvariant()}, {startSql}, {endSql})",

            Core.DatabaseProvider.Postgres => BuildPostgresDateDiffSql(unit, startSql, endSql),

            Core.DatabaseProvider.SQLite => BuildSqliteDateDiffSql(unit, startSql, endSql),

            _ => $"EXTRACT(DAY FROM ({endSql}::timestamp - {startSql}::timestamp))",
        };

        return new RawSqlExpr(sql, PinDataType.Number);
    }

    private static ISqlExpression CompileDatePart(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression valueExpr = ctx.ResolveInput(node.Id, "value", PinDataType.DateTime);
        string part = NormalizeDateUnit(GetParam(node, "part", "year"));
        string valueSql = valueExpr.Emit(ctx.EmitContext);

        string sql = ctx.EmitContext.Provider switch
        {
            Core.DatabaseProvider.SqlServer => $"DATEPART({part.ToUpperInvariant()}, {valueSql})",
            Core.DatabaseProvider.MySql => $"EXTRACT({part.ToUpperInvariant()} FROM {valueSql})",
            Core.DatabaseProvider.Postgres => $"EXTRACT({part.ToUpperInvariant()} FROM {valueSql})",
            Core.DatabaseProvider.SQLite => BuildSqliteDatePartSql(part, valueSql),
            _ => $"EXTRACT({part.ToUpperInvariant()} FROM {valueSql})",
        };

        return new RawSqlExpr(sql, PinDataType.Number);
    }

    private static ISqlExpression CompileDateFormat(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression valueExpr = ctx.ResolveInput(node.Id, "value", PinDataType.DateTime);
        string pattern = GetParam(node, "format", "yyyy-MM-dd");
        string valueSql = valueExpr.Emit(ctx.EmitContext);

        string sql = ctx.EmitContext.Provider switch
        {
            Core.DatabaseProvider.SqlServer =>
                $"FORMAT({valueSql}, {EmitContext.QuoteLiteral(pattern)})",

            Core.DatabaseProvider.MySql =>
                $"DATE_FORMAT({valueSql}, {EmitContext.QuoteLiteral(ConvertToMySqlDateFormat(pattern))})",

            Core.DatabaseProvider.Postgres =>
                $"TO_CHAR({valueSql}, {EmitContext.QuoteLiteral(ConvertToPostgresDateFormat(pattern))})",

            Core.DatabaseProvider.SQLite =>
                $"strftime({EmitContext.QuoteLiteral(ConvertToSqliteDateFormat(pattern))}, {valueSql})",

            _ => $"TO_CHAR({valueSql}, {EmitContext.QuoteLiteral(ConvertToPostgresDateFormat(pattern))})",
        };

        return new RawSqlExpr(sql, PinDataType.Text);
    }

    private static ISqlExpression ResolveInputOrNumberParam(
        NodeInstance node,
        INodeCompilationContext ctx,
        string pinName,
        string defaultValue
    )
    {
        var wire = ctx.Graph.GetSingleInputConnection(node.Id, pinName);
        if (wire is not null)
            return ctx.Resolve(wire.FromNodeId, wire.FromPinName);

        string raw = GetParam(node, pinName, defaultValue);
        return double.TryParse(
            raw,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out double parsed
        )
            ? new NumberLiteralExpr(parsed)
            : new RawSqlExpr(raw, PinDataType.Number);
    }

    private static string GetParam(NodeInstance node, string name, string fallback) =>
        node.Parameters.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;

    private static string NormalizeDateUnit(string raw)
    {
        string unit = raw.Trim().ToLowerInvariant();
        return unit switch
        {
            "years" => "year",
            "months" => "month",
            "days" => "day",
            "hours" => "hour",
            "minutes" => "minute",
            "seconds" => "second",
            _ => unit,
        };
    }

    private static string BuildPostgresDateDiffSql(string unit, string startSql, string endSql) =>
        unit switch
        {
            "second" => $"EXTRACT(EPOCH FROM ({endSql}::timestamp - {startSql}::timestamp))",
            "minute" =>
                $"EXTRACT(EPOCH FROM ({endSql}::timestamp - {startSql}::timestamp)) / 60",
            "hour" =>
                $"EXTRACT(EPOCH FROM ({endSql}::timestamp - {startSql}::timestamp)) / 3600",
            "month" =>
                $"(EXTRACT(YEAR FROM AGE({endSql}::timestamp, {startSql}::timestamp)) * 12 + EXTRACT(MONTH FROM AGE({endSql}::timestamp, {startSql}::timestamp)))",
            "year" =>
                $"EXTRACT(YEAR FROM AGE({endSql}::timestamp, {startSql}::timestamp))",
            _ => $"EXTRACT(DAY FROM ({endSql}::timestamp - {startSql}::timestamp))",
        };

    private static string BuildSqliteDateDiffSql(string unit, string startSql, string endSql) =>
        unit switch
        {
            "second" => $"(strftime('%s', {endSql}) - strftime('%s', {startSql}))",
            "minute" => $"((strftime('%s', {endSql}) - strftime('%s', {startSql})) / 60)",
            "hour" => $"((strftime('%s', {endSql}) - strftime('%s', {startSql})) / 3600)",
            "month" =>
                $"(((CAST(strftime('%Y', {endSql}) AS INTEGER) - CAST(strftime('%Y', {startSql}) AS INTEGER)) * 12) + (CAST(strftime('%m', {endSql}) AS INTEGER) - CAST(strftime('%m', {startSql}) AS INTEGER)))",
            "year" =>
                $"(CAST(strftime('%Y', {endSql}) AS INTEGER) - CAST(strftime('%Y', {startSql}) AS INTEGER))",
            _ => $"CAST((julianday({endSql}) - julianday({startSql})) AS INTEGER)",
        };

    private static string BuildSqliteDatePartSql(string part, string valueSql) =>
        part switch
        {
            "year" => $"CAST(strftime('%Y', {valueSql}) AS INTEGER)",
            "month" => $"CAST(strftime('%m', {valueSql}) AS INTEGER)",
            "day" => $"CAST(strftime('%d', {valueSql}) AS INTEGER)",
            "hour" => $"CAST(strftime('%H', {valueSql}) AS INTEGER)",
            "minute" => $"CAST(strftime('%M', {valueSql}) AS INTEGER)",
            "second" => $"CAST(strftime('%S', {valueSql}) AS INTEGER)",
            _ => $"CAST(strftime('%Y', {valueSql}) AS INTEGER)",
        };

    private static string ConvertToMySqlDateFormat(string pattern) =>
        pattern
            .Replace("yyyy", "%Y", StringComparison.OrdinalIgnoreCase)
            .Replace("MM", "%m", StringComparison.OrdinalIgnoreCase)
            .Replace("dd", "%d", StringComparison.OrdinalIgnoreCase)
            .Replace("HH", "%H", StringComparison.OrdinalIgnoreCase)
            .Replace("mm", "%i", StringComparison.OrdinalIgnoreCase)
            .Replace("ss", "%s", StringComparison.OrdinalIgnoreCase);

    private static string ConvertToPostgresDateFormat(string pattern) =>
        pattern
            .Replace("yyyy", "YYYY", StringComparison.OrdinalIgnoreCase)
            .Replace("MM", "MM", StringComparison.OrdinalIgnoreCase)
            .Replace("dd", "DD", StringComparison.OrdinalIgnoreCase)
            .Replace("HH", "HH24", StringComparison.OrdinalIgnoreCase)
            .Replace("mm", "MI", StringComparison.OrdinalIgnoreCase)
            .Replace("ss", "SS", StringComparison.OrdinalIgnoreCase);

    private static string ConvertToSqliteDateFormat(string pattern) =>
        pattern
            .Replace("yyyy", "%Y", StringComparison.OrdinalIgnoreCase)
            .Replace("MM", "%m", StringComparison.OrdinalIgnoreCase)
            .Replace("dd", "%d", StringComparison.OrdinalIgnoreCase)
            .Replace("HH", "%H", StringComparison.OrdinalIgnoreCase)
            .Replace("mm", "%M", StringComparison.OrdinalIgnoreCase)
            .Replace("ss", "%S", StringComparison.OrdinalIgnoreCase);
}
