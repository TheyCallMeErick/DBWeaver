using VisualSqlArchitect.Core;
using VisualSqlArchitect.Registry;

namespace VisualSqlArchitect.Nodes;

// ═════════════════════════════════════════════════════════════════════════════
// EMIT CONTEXT
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Passed through every expression during compilation.
/// Carries the provider dialect and the function registry so expressions
/// can produce correct SQL without knowing the database themselves.
/// </summary>
public sealed class EmitContext(DatabaseProvider provider, ISqlFunctionRegistry registry)
{
    public DatabaseProvider Provider { get; } = provider;
    public ISqlFunctionRegistry Registry { get; } = registry;

    public string QuoteIdentifier(string id) =>
        Provider switch
        {
            DatabaseProvider.SqlServer => $"[{id}]",
            DatabaseProvider.MySql => $"`{id}`",
            DatabaseProvider.Postgres => $"\"{id}\"",
            _ => id,
        };

    public static string QuoteLiteral(string value) => $"'{value.Replace("'", "''")}'";
}

// ═════════════════════════════════════════════════════════════════════════════
// EXPRESSION INTERFACE
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A composable SQL fragment. Every Atomic Node emits one.
/// Expressions form a tree that mirrors the canvas node graph:
///
///   UPPER(orders.email)
///   └─ FunctionCallExpr("UPPER")
///      └─ ColumnExpr("orders", "email")
///
///   orders.total BETWEEN 100 AND 500
///   └─ BetweenExpr(negate:false)
///      ├─ ColumnExpr("orders", "total")
///      ├─ LiteralExpr("100")
///      └─ LiteralExpr("500")
/// </summary>
public interface ISqlExpression
{
    /// <summary>Compiles this expression node into a SQL fragment string.</summary>
    string Emit(EmitContext ctx);

    /// <summary>
    /// Semantic data type of this expression's output.
    /// Used to validate pin connections at design time.
    /// </summary>
    PinDataType OutputType { get; }
}

// ═════════════════════════════════════════════════════════════════════════════
// PIN DATA TYPES  (for canvas-side type-checking)
// ═════════════════════════════════════════════════════════════════════════════

public enum PinDataType
{
    Text,
    Integer,
    Decimal,
    Number,
    Boolean,
    DateTime,
    Json,
    ColumnRef,
    ColumnSet,
    RowSet,
    Expression, // untyped SQL fragment — accepted by any slot
}

// ═════════════════════════════════════════════════════════════════════════════
// LEAF EXPRESSIONS
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>A raw string literal: 'hello', 42, true, NULL</summary>
public sealed record LiteralExpr(string RawValue, PinDataType OutputType = PinDataType.Expression)
    : ISqlExpression
{
    public string Emit(EmitContext ctx) => RawValue;
}

/// <summary>A quoted string constant: the canvas writes 'hello world'</summary>
public sealed record StringLiteralExpr(string Value) : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Text;

    public string Emit(EmitContext ctx) => EmitContext.QuoteLiteral(Value);
}

/// <summary>A numeric constant: 3.14, -7, 0</summary>
public sealed record NumberLiteralExpr(double Value) : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Number;

    public string Emit(EmitContext ctx) =>
        Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>NULL sentinel.</summary>
public sealed record NullExpr : ISqlExpression
{
    public static readonly NullExpr Instance = new();
    public PinDataType OutputType => PinDataType.Expression;

    public string Emit(EmitContext ctx) => "NULL";
}

/// <summary>
/// A table column reference: table.column — the "output pin" of a DataSource node.
/// Every column on the canvas becomes one of these.
/// </summary>
public sealed record ColumnExpr(
    string TableAlias,
    string ColumnName,
    PinDataType OutputType = PinDataType.ColumnRef
) : ISqlExpression
{
    public string Emit(EmitContext ctx)
    {
        if (ColumnName == "*")
            return string.IsNullOrEmpty(TableAlias) ? "*" : $"{ctx.QuoteIdentifier(TableAlias)}.*";

        return string.IsNullOrEmpty(TableAlias)
            ? ctx.QuoteIdentifier(ColumnName)
            : $"{ctx.QuoteIdentifier(TableAlias)}.{ctx.QuoteIdentifier(ColumnName)}";
    }
}

/// <summary>
/// A CTE source reference used in FROM/JOIN clauses: cte_name [alias].
/// </summary>
public sealed record CteReferenceExpr(string CteName, string? Alias = null) : ISqlExpression
{
    public PinDataType OutputType => PinDataType.RowSet;

    public string Emit(EmitContext ctx)
    {
        string cte = CteName.Trim();
        if (string.IsNullOrWhiteSpace(Alias))
            return cte;

        return $"{cte} {Alias.Trim()}";
    }
}

/// <summary>
/// Passes a raw SQL fragment through unchanged (escape hatch for advanced users).
/// </summary>
public sealed record RawSqlExpr(string Sql, PinDataType OutputType = PinDataType.Expression)
    : ISqlExpression
{
    public string Emit(EmitContext ctx) => Sql;
}

// ═════════════════════════════════════════════════════════════════════════════
// FUNCTION CALL EXPRESSION  (registry-dispatched)
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Calls a canonical function through the <see cref="ISqlFunctionRegistry"/>.
/// Each child expression is emitted first; the resulting strings are passed
/// as args to the registry.
///
/// Example: FunctionCallExpr(SqlFn.Upper, [ColumnExpr("users","email")])
///   Postgres/MySQL/SQL Server → UPPER("users"."email")
/// </summary>
public sealed record FunctionCallExpr(
    string FunctionName,
    IReadOnlyList<ISqlExpression> Args,
    PinDataType OutputType = PinDataType.Expression
) : ISqlExpression
{
    public string Emit(EmitContext ctx)
    {
        string[] emittedArgs = [.. Args.Select(a => a.Emit(ctx))];
        return ctx.Registry.GetFunction(FunctionName, emittedArgs);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// CAST EXPRESSION
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Canonical CAST — every provider uses the SQL-standard CAST(x AS type) syntax.
/// The target type is automatically translated to the provider's dialect.
/// </summary>
public sealed record CastExpr(ISqlExpression Input, CastTargetType TargetType) : ISqlExpression
{
    public PinDataType OutputType =>
        TargetType switch
        {
            CastTargetType.Text => PinDataType.Text,
            CastTargetType.Integer
            or CastTargetType.BigInt
            or CastTargetType.Decimal
            or CastTargetType.Float => PinDataType.Number,
            CastTargetType.Boolean => PinDataType.Boolean,
            CastTargetType.Date or CastTargetType.DateTime or CastTargetType.Timestamp =>
                PinDataType.DateTime,
            _ => PinDataType.Expression,
        };

    public string Emit(EmitContext ctx)
    {
        string inner = Input.Emit(ctx);
        string providerType = TranslateType(ctx.Provider);
        return $"CAST({inner} AS {providerType})";
    }

    private string TranslateType(DatabaseProvider p) =>
        (TargetType, p) switch
        {
            (CastTargetType.Text, DatabaseProvider.SqlServer) => "NVARCHAR(MAX)",
            (CastTargetType.Text, _) => "TEXT",
            (CastTargetType.Integer, DatabaseProvider.Postgres) => "INTEGER",
            (CastTargetType.Integer, _) => "INT",
            (CastTargetType.BigInt, _) => "BIGINT",
            (CastTargetType.Decimal, _) => "DECIMAL(18,4)",
            (CastTargetType.Float, DatabaseProvider.Postgres) => "DOUBLE PRECISION",
            (CastTargetType.Float, _) => "FLOAT",
            (CastTargetType.Boolean, DatabaseProvider.SqlServer) => "BIT",
            (CastTargetType.Boolean, _) => "BOOLEAN",
            (CastTargetType.Date, _) => "DATE",
            (CastTargetType.DateTime, DatabaseProvider.Postgres) => "TIMESTAMP",
            (CastTargetType.DateTime, _) => "DATETIME",
            (CastTargetType.Timestamp, DatabaseProvider.SqlServer) => "DATETIMEOFFSET",
            (CastTargetType.Timestamp, _) => "TIMESTAMPTZ",
            (CastTargetType.Uuid, DatabaseProvider.SqlServer) => "UNIQUEIDENTIFIER",
            (CastTargetType.Uuid, _) => "UUID",
            _ => TargetType.ToString().ToUpperInvariant(),
        };
}

public enum CastTargetType
{
    Text,
    Integer,
    BigInt,
    Decimal,
    Float,
    Boolean,
    Date,
    DateTime,
    Timestamp,
    Uuid,
}

// ═════════════════════════════════════════════════════════════════════════════
// COMPARISON EXPRESSIONS
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Standard binary comparison: left OP right</summary>
public sealed record ComparisonExpr(
    ISqlExpression Left,
    ComparisonOperator Op,
    ISqlExpression Right
) : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Boolean;

    public string Emit(EmitContext ctx)
    {
        string l = Left.Emit(ctx);
        string r = Right.Emit(ctx);
        string op = Op switch
        {
            ComparisonOperator.Eq => "=",
            ComparisonOperator.Neq => "<>",
            ComparisonOperator.Gt => ">",
            ComparisonOperator.Gte => ">=",
            ComparisonOperator.Lt => "<",
            ComparisonOperator.Lte => "<=",
            ComparisonOperator.Like => "LIKE",
            ComparisonOperator.NotLike => "NOT LIKE",
            _ => throw new NotSupportedException($"Unknown operator: {Op}"),
        };
        return $"({l} {op} {r})";
    }
}

public enum ComparisonOperator
{
    Eq,
    Neq,
    Gt,
    Gte,
    Lt,
    Lte,
    Like,
    NotLike,
}

/// <summary>BETWEEN … AND … or NOT BETWEEN</summary>
public sealed record BetweenExpr(
    ISqlExpression Input,
    ISqlExpression Lo,
    ISqlExpression Hi,
    bool Negate = false
) : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Boolean;

    public string Emit(EmitContext ctx)
    {
        string keyword = Negate ? "NOT BETWEEN" : "BETWEEN";
        return $"({Input.Emit(ctx)} {keyword} {Lo.Emit(ctx)} AND {Hi.Emit(ctx)})";
    }
}

/// <summary>IS NULL / IS NOT NULL</summary>
public sealed record IsNullExpr(ISqlExpression Input, bool Negate = false) : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Boolean;

    public string Emit(EmitContext ctx)
    {
        string keyword = Negate ? "IS NOT NULL" : "IS NULL";
        return $"({Input.Emit(ctx)} {keyword})";
    }
}

/// <summary>EXISTS (subquery) / NOT EXISTS (subquery).</summary>
public sealed record ExistsExpr(string SubquerySql, bool Negate = false) : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Boolean;

    public string Emit(EmitContext ctx)
    {
        string body = (SubquerySql ?? string.Empty).Trim().TrimEnd(';');
        if (string.IsNullOrWhiteSpace(body))
            body = "SELECT 1";

        string keyword = Negate ? "NOT EXISTS" : "EXISTS";
        return $"{keyword} ({body})";
    }
}

/// <summary>value IN (subquery) / value NOT IN (subquery).</summary>
public sealed record InSubqueryExpr(ISqlExpression Value, string SubquerySql, bool Negate = false)
    : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Boolean;

    public string Emit(EmitContext ctx)
    {
        string body = (SubquerySql ?? string.Empty).Trim().TrimEnd(';');
        if (string.IsNullOrWhiteSpace(body))
            body = "SELECT NULL";

        string keyword = Negate ? "NOT IN" : "IN";
        return $"({Value.Emit(ctx)} {keyword} ({body}))";
    }
}

/// <summary>Scalar subquery used as an expression: (SELECT ...).</summary>
public sealed record ScalarSubqueryExpr(string SubquerySql) : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Expression;

    public string Emit(EmitContext ctx)
    {
        string body = (SubquerySql ?? string.Empty).Trim().TrimEnd(';');
        if (string.IsNullOrWhiteSpace(body))
            body = "SELECT NULL";
        return $"({body})";
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// LOGICAL GATE EXPRESSIONS
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>AND / OR with variadic operands.</summary>
public sealed record LogicGateExpr(LogicOperator Op, IReadOnlyList<ISqlExpression> Operands)
    : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Boolean;

    public string Emit(EmitContext ctx)
    {
        if (Operands.Count == 0)
            return Op == LogicOperator.And ? "TRUE" : "FALSE";
        if (Operands.Count == 1)
            return Operands[0].Emit(ctx);

        string keyword = Op == LogicOperator.And ? " AND " : " OR ";
        IEnumerable<string> parts = Operands.Select(o => o.Emit(ctx));
        return $"({string.Join(keyword, parts)})";
    }
}

public enum LogicOperator
{
    And,
    Or,
}

/// <summary>NOT — single operand.</summary>
public sealed record NotExpr(ISqlExpression Operand) : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Boolean;

    public string Emit(EmitContext ctx) => $"(NOT {Operand.Emit(ctx)})";
}

// ═════════════════════════════════════════════════════════════════════════════
// ALIAS EXPRESSION  (wraps any expression with AS alias)
// ═════════════════════════════════════════════════════════════════════════════

public sealed record AliasExpr(ISqlExpression Inner, string Alias) : ISqlExpression
{
    public PinDataType OutputType => Inner.OutputType;

    public string Emit(EmitContext ctx) => $"{Inner.Emit(ctx)} AS {ctx.QuoteIdentifier(Alias)}";
}

// ═════════════════════════════════════════════════════════════════════════════
// AGGREGATE EXPRESSION
// ═════════════════════════════════════════════════════════════════════════════

public sealed record AggregateExpr(
    AggregateFunction Function,
    ISqlExpression? Inner, // null for COUNT(*)
    bool Distinct = false
) : ISqlExpression
{
    public PinDataType OutputType =>
        Function == AggregateFunction.Count
            ? PinDataType.Number
            : Inner?.OutputType ?? PinDataType.Number;

    public string Emit(EmitContext ctx)
    {
        string fn = Function.ToString().ToUpperInvariant();
        if (Inner is null)
            return $"{fn}(*)";
        string distinctKw = Distinct ? "DISTINCT " : "";
        return $"{fn}({distinctKw}{Inner.Emit(ctx)})";
    }
}

public enum AggregateFunction
{
    Count,
    Sum,
    Avg,
    Min,
    Max,
}

// ═════════════════════════════════════════════════════════════════════════════
// WINDOW FUNCTION EXPRESSIONS
// ═════════════════════════════════════════════════════════════════════════════

public enum WindowFunctionKind
{
    RowNumber,
    Rank,
    DenseRank,
    Ntile,
    Lag,
    Lead,
    FirstValue,
    LastValue,
    SumOver,
    AvgOver,
    MinOver,
    MaxOver,
    CountOver,
}

public enum WindowFrameSpec
{
    None,
    RowsUnboundedPrecedingToCurrentRow,
    RowsCurrentRowToUnboundedFollowing,
    RowsCustomBetween,
}

public enum WindowFrameBoundKind
{
    UnboundedPreceding,
    Preceding,
    CurrentRow,
    Following,
    UnboundedFollowing,
}

public sealed record WindowFrameBound(WindowFrameBoundKind Kind, int? Offset = null)
{
    public string Emit()
    {
        return Kind switch
        {
            WindowFrameBoundKind.UnboundedPreceding => "UNBOUNDED PRECEDING",
            WindowFrameBoundKind.Preceding => $"{Math.Max(0, Offset ?? 0)} PRECEDING",
            WindowFrameBoundKind.CurrentRow => "CURRENT ROW",
            WindowFrameBoundKind.Following => $"{Math.Max(0, Offset ?? 0)} FOLLOWING",
            WindowFrameBoundKind.UnboundedFollowing => "UNBOUNDED FOLLOWING",
            _ => "CURRENT ROW",
        };
    }
}

public sealed record WindowOrderByExpr(ISqlExpression Expr, bool Desc = false);

public sealed record WindowOverExpr(
    IReadOnlyList<ISqlExpression> PartitionBy,
    IReadOnlyList<WindowOrderByExpr> OrderBy,
    WindowFrameSpec Frame = WindowFrameSpec.None,
    WindowFrameBound? CustomFrameStart = null,
    WindowFrameBound? CustomFrameEnd = null,
    bool ForceSqlServerDummyOrderBy = false
)
{
    public string Emit(EmitContext ctx)
    {
        var parts = new List<string>();

        if (PartitionBy.Count > 0)
            parts.Add($"PARTITION BY {string.Join(", ", PartitionBy.Select(p => p.Emit(ctx)))}");

        bool hasOrderBy = OrderBy.Count > 0;
        if (hasOrderBy)
        {
            parts.Add(
                $"ORDER BY {string.Join(", ", OrderBy.Select(o => o.Desc ? $"{o.Expr.Emit(ctx)} DESC" : o.Expr.Emit(ctx)))}"
            );
        }
        else if (ForceSqlServerDummyOrderBy && ctx.Provider == DatabaseProvider.SqlServer)
        {
            parts.Add("ORDER BY (SELECT 1)");
            hasOrderBy = true;
        }

        string? frameSql = Frame switch
        {
            WindowFrameSpec.RowsUnboundedPrecedingToCurrentRow =>
                "ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW",
            WindowFrameSpec.RowsCurrentRowToUnboundedFollowing =>
                "ROWS BETWEEN CURRENT ROW AND UNBOUNDED FOLLOWING",
            WindowFrameSpec.RowsCustomBetween when CustomFrameStart is not null && CustomFrameEnd is not null =>
                $"ROWS BETWEEN {CustomFrameStart.Emit()} AND {CustomFrameEnd.Emit()}",
            _ => null,
        };

        if (!string.IsNullOrWhiteSpace(frameSql) && hasOrderBy)
            parts.Add(frameSql);

        return string.Join(" ", parts);
    }
}

public sealed record WindowFunctionExpr(
    WindowFunctionKind Kind,
    WindowOverExpr Over,
    ISqlExpression? Value = null,
    int? Offset = null,
    ISqlExpression? DefaultValue = null,
    int? NtileGroups = null
) : ISqlExpression
{
    public PinDataType OutputType => Kind switch
    {
        WindowFunctionKind.RowNumber
        or WindowFunctionKind.Rank
        or WindowFunctionKind.DenseRank
        or WindowFunctionKind.Ntile
        or WindowFunctionKind.SumOver
        or WindowFunctionKind.AvgOver
        or WindowFunctionKind.CountOver => PinDataType.Number,

        WindowFunctionKind.Lag
        or WindowFunctionKind.Lead
        or WindowFunctionKind.FirstValue
        or WindowFunctionKind.LastValue
        or WindowFunctionKind.MinOver
        or WindowFunctionKind.MaxOver => Value?.OutputType ?? PinDataType.Expression,

        _ => PinDataType.Expression,
    };

    public string Emit(EmitContext ctx)
    {
        string inner = Kind switch
        {
            WindowFunctionKind.RowNumber => "ROW_NUMBER()",
            WindowFunctionKind.Rank => "RANK()",
            WindowFunctionKind.DenseRank => "DENSE_RANK()",
            WindowFunctionKind.Ntile => $"NTILE({Math.Max(1, NtileGroups ?? 4)})",
            WindowFunctionKind.Lag => BuildLagLead("LAG", ctx),
            WindowFunctionKind.Lead => BuildLagLead("LEAD", ctx),
            WindowFunctionKind.FirstValue =>
                $"FIRST_VALUE({(Value ?? throw new NotSupportedException("FirstValue requires 'value' input.")).Emit(ctx)})",
            WindowFunctionKind.LastValue =>
                $"LAST_VALUE({(Value ?? throw new NotSupportedException("LastValue requires 'value' input.")).Emit(ctx)})",
            WindowFunctionKind.SumOver =>
                $"SUM({(Value ?? throw new NotSupportedException("SumOver requires 'value' input.")).Emit(ctx)})",
            WindowFunctionKind.AvgOver =>
                $"AVG({(Value ?? throw new NotSupportedException("AvgOver requires 'value' input.")).Emit(ctx)})",
            WindowFunctionKind.MinOver =>
                $"MIN({(Value ?? throw new NotSupportedException("MinOver requires 'value' input.")).Emit(ctx)})",
            WindowFunctionKind.MaxOver =>
                $"MAX({(Value ?? throw new NotSupportedException("MaxOver requires 'value' input.")).Emit(ctx)})",
            WindowFunctionKind.CountOver =>
                Value is null ? "COUNT(*)" : $"COUNT({Value.Emit(ctx)})",
            _ => throw new NotSupportedException($"Unsupported window function kind: {Kind}"),
        };

        return $"{inner} OVER ({Over.Emit(ctx)})";
    }

    private string BuildLagLead(string fn, EmitContext ctx)
    {
        ISqlExpression value = Value
            ?? throw new NotSupportedException($"{fn} requires 'value' input.");

        int offset = Offset.HasValue && Offset.Value > 0 ? Offset.Value : 1;
        string valueSql = value.Emit(ctx);

        return DefaultValue is null
            ? $"{fn}({valueSql}, {offset})"
            : $"{fn}({valueSql}, {offset}, {DefaultValue.Emit(ctx)})";
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// CASE / WHEN EXPRESSION
// ═════════════════════════════════════════════════════════════════════════════

public sealed record WhenClause(ISqlExpression Condition, ISqlExpression Result);

public sealed record CaseExpr(IReadOnlyList<WhenClause> Whens, ISqlExpression? Else = null)
    : ISqlExpression
{
    public PinDataType OutputType => Else?.OutputType ?? PinDataType.Expression;

    public string Emit(EmitContext ctx)
    {
        var sb = new System.Text.StringBuilder("CASE");
        foreach (WhenClause w in Whens)
            sb.Append($" WHEN {w.Condition.Emit(ctx)} THEN {w.Result.Emit(ctx)}");
        if (Else is not null)
            sb.Append($" ELSE {Else.Emit(ctx)}");
        sb.Append(" END");
        return sb.ToString();
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// TOP / LIMIT EXPRESSION
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// TOP / LIMIT clause: restricts the result set to N rows.
/// Emits as TOP N (SQL Server) or LIMIT N (PostgreSQL/MySQL).
/// </summary>
public sealed record TopExpr(ISqlExpression Result, ISqlExpression Count) : ISqlExpression
{
    public PinDataType OutputType => Result.OutputType;

    public string Emit(EmitContext ctx)
    {
        string resultSql = Result.Emit(ctx);

        _ = Count.Emit(ctx);
        // Note: The actual TOP/LIMIT syntax is typically handled at the SELECT level,
        // but this expression wraps both the result expression and the count.
        return resultSql; // Return the result, count is used separately in SELECT compilation
    }
}
