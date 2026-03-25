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
public sealed class EmitContext
{
    public DatabaseProvider Provider { get; }
    public ISqlFunctionRegistry Registry { get; }

    public EmitContext(DatabaseProvider provider, ISqlFunctionRegistry registry)
    {
        Provider = provider;
        Registry = registry;
    }

    public string QuoteIdentifier(string id) => Provider switch
    {
        DatabaseProvider.SqlServer => $"[{id}]",
        DatabaseProvider.MySql     => $"`{id}`",
        DatabaseProvider.Postgres  => $"\"{id}\"",
        _                          => id
    };

    public string QuoteLiteral(string value) => $"'{value.Replace("'", "''")}'";
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
    Any,
    Text,
    Number,
    Boolean,
    DateTime,
    Json,
    Expression   // untyped SQL fragment — accepted by any slot
}

// ═════════════════════════════════════════════════════════════════════════════
// LEAF EXPRESSIONS
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>A raw string literal: 'hello', 42, true, NULL</summary>
public sealed record LiteralExpr(string RawValue, PinDataType OutputType = PinDataType.Any)
    : ISqlExpression
{
    public string Emit(EmitContext ctx) => RawValue;
}

/// <summary>A quoted string constant: the canvas writes 'hello world'</summary>
public sealed record StringLiteralExpr(string Value) : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Text;
    public string Emit(EmitContext ctx) => ctx.QuoteLiteral(Value);
}

/// <summary>A numeric constant: 3.14, -7, 0</summary>
public sealed record NumberLiteralExpr(double Value) : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Number;
    public string Emit(EmitContext ctx) => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>NULL sentinel.</summary>
public sealed record NullExpr : ISqlExpression
{
    public static readonly NullExpr Instance = new();
    public PinDataType OutputType => PinDataType.Any;
    public string Emit(EmitContext ctx) => "NULL";
}

/// <summary>
/// A table column reference: table.column — the "output pin" of a DataSource node.
/// Every column on the canvas becomes one of these.
/// </summary>
public sealed record ColumnExpr(string TableAlias, string ColumnName,
    PinDataType OutputType = PinDataType.Any) : ISqlExpression
{
    public string Emit(EmitContext ctx) =>
        string.IsNullOrEmpty(TableAlias)
            ? ctx.QuoteIdentifier(ColumnName)
            : $"{ctx.QuoteIdentifier(TableAlias)}.{ctx.QuoteIdentifier(ColumnName)}";
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
    PinDataType OutputType = PinDataType.Any) : ISqlExpression
{
    public string Emit(EmitContext ctx)
    {
        var emittedArgs = Args.Select(a => a.Emit(ctx)).ToArray();
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
public sealed record CastExpr(
    ISqlExpression Input,
    CastTargetType TargetType) : ISqlExpression
{
    public PinDataType OutputType => TargetType switch
    {
        CastTargetType.Text      => PinDataType.Text,
        CastTargetType.Integer
            or CastTargetType.BigInt
            or CastTargetType.Decimal
            or CastTargetType.Float   => PinDataType.Number,
        CastTargetType.Boolean        => PinDataType.Boolean,
        CastTargetType.Date
            or CastTargetType.DateTime
            or CastTargetType.Timestamp => PinDataType.DateTime,
        _                             => PinDataType.Any
    };

    public string Emit(EmitContext ctx)
    {
        var inner       = Input.Emit(ctx);
        var providerType = TranslateType(ctx.Provider);
        return $"CAST({inner} AS {providerType})";
    }

    private string TranslateType(DatabaseProvider p) => (TargetType, p) switch
    {
        (CastTargetType.Text,      DatabaseProvider.SqlServer) => "NVARCHAR(MAX)",
        (CastTargetType.Text,      _)                          => "TEXT",
        (CastTargetType.Integer,   DatabaseProvider.Postgres)  => "INTEGER",
        (CastTargetType.Integer,   _)                          => "INT",
        (CastTargetType.BigInt,    _)                          => "BIGINT",
        (CastTargetType.Decimal,   _)                          => "DECIMAL(18,4)",
        (CastTargetType.Float,     DatabaseProvider.Postgres)  => "DOUBLE PRECISION",
        (CastTargetType.Float,     _)                          => "FLOAT",
        (CastTargetType.Boolean,   DatabaseProvider.SqlServer) => "BIT",
        (CastTargetType.Boolean,   _)                          => "BOOLEAN",
        (CastTargetType.Date,      _)                          => "DATE",
        (CastTargetType.DateTime,  DatabaseProvider.Postgres)  => "TIMESTAMP",
        (CastTargetType.DateTime,  _)                          => "DATETIME",
        (CastTargetType.Timestamp, DatabaseProvider.SqlServer) => "DATETIMEOFFSET",
        (CastTargetType.Timestamp, _)                          => "TIMESTAMPTZ",
        (CastTargetType.Uuid,      DatabaseProvider.SqlServer) => "UNIQUEIDENTIFIER",
        (CastTargetType.Uuid,      _)                          => "UUID",
        _                                                       => TargetType.ToString().ToUpperInvariant()
    };
}

public enum CastTargetType
{
    Text, Integer, BigInt, Decimal, Float,
    Boolean, Date, DateTime, Timestamp, Uuid
}

// ═════════════════════════════════════════════════════════════════════════════
// COMPARISON EXPRESSIONS
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Standard binary comparison: left OP right</summary>
public sealed record ComparisonExpr(
    ISqlExpression Left,
    ComparisonOperator Op,
    ISqlExpression Right) : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Boolean;

    public string Emit(EmitContext ctx)
    {
        var l = Left.Emit(ctx);
        var r = Right.Emit(ctx);
        var op = Op switch
        {
            ComparisonOperator.Eq      => "=",
            ComparisonOperator.Neq     => "<>",
            ComparisonOperator.Gt      => ">",
            ComparisonOperator.Gte     => ">=",
            ComparisonOperator.Lt      => "<",
            ComparisonOperator.Lte     => "<=",
            ComparisonOperator.Like    => "LIKE",
            ComparisonOperator.NotLike => "NOT LIKE",
            _ => throw new NotSupportedException($"Unknown operator: {Op}")
        };
        return $"({l} {op} {r})";
    }
}

public enum ComparisonOperator { Eq, Neq, Gt, Gte, Lt, Lte, Like, NotLike }

/// <summary>BETWEEN … AND … or NOT BETWEEN</summary>
public sealed record BetweenExpr(
    ISqlExpression Input,
    ISqlExpression Lo,
    ISqlExpression Hi,
    bool Negate = false) : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Boolean;

    public string Emit(EmitContext ctx)
    {
        var keyword = Negate ? "NOT BETWEEN" : "BETWEEN";
        return $"({Input.Emit(ctx)} {keyword} {Lo.Emit(ctx)} AND {Hi.Emit(ctx)})";
    }
}

/// <summary>IS NULL / IS NOT NULL</summary>
public sealed record IsNullExpr(ISqlExpression Input, bool Negate = false) : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Boolean;
    public string Emit(EmitContext ctx)
    {
        var keyword = Negate ? "IS NOT NULL" : "IS NULL";
        return $"({Input.Emit(ctx)} {keyword})";
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// LOGICAL GATE EXPRESSIONS
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>AND / OR with variadic operands.</summary>
public sealed record LogicGateExpr(
    LogicOperator Op,
    IReadOnlyList<ISqlExpression> Operands) : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Boolean;

    public string Emit(EmitContext ctx)
    {
        if (Operands.Count == 0) return Op == LogicOperator.And ? "TRUE" : "FALSE";
        if (Operands.Count == 1) return Operands[0].Emit(ctx);

        var keyword = Op == LogicOperator.And ? " AND " : " OR ";
        var parts   = Operands.Select(o => o.Emit(ctx));
        return $"({string.Join(keyword, parts)})";
    }
}

public enum LogicOperator { And, Or }

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
    public string Emit(EmitContext ctx) =>
        $"{Inner.Emit(ctx)} AS {ctx.QuoteIdentifier(Alias)}";
}

// ═════════════════════════════════════════════════════════════════════════════
// AGGREGATE EXPRESSION
// ═════════════════════════════════════════════════════════════════════════════

public sealed record AggregateExpr(
    AggregateFunction Function,
    ISqlExpression? Inner,    // null for COUNT(*)
    bool Distinct = false) : ISqlExpression
{
    public PinDataType OutputType => Function == AggregateFunction.Count
        ? PinDataType.Number
        : Inner?.OutputType ?? PinDataType.Number;

    public string Emit(EmitContext ctx)
    {
        var fn = Function.ToString().ToUpperInvariant();
        if (Inner is null) return $"{fn}(*)";
        var distinctKw = Distinct ? "DISTINCT " : "";
        return $"{fn}({distinctKw}{Inner.Emit(ctx)})";
    }
}

public enum AggregateFunction { Count, Sum, Avg, Min, Max }

// ═════════════════════════════════════════════════════════════════════════════
// CASE / WHEN EXPRESSION
// ═════════════════════════════════════════════════════════════════════════════

public sealed record WhenClause(ISqlExpression Condition, ISqlExpression Result);

public sealed record CaseExpr(
    IReadOnlyList<WhenClause> Whens,
    ISqlExpression? Else = null) : ISqlExpression
{
    public PinDataType OutputType => Else?.OutputType ?? PinDataType.Any;

    public string Emit(EmitContext ctx)
    {
        var sb = new System.Text.StringBuilder("CASE");
        foreach (var w in Whens)
            sb.Append($" WHEN {w.Condition.Emit(ctx)} THEN {w.Result.Emit(ctx)}");
        if (Else is not null)
            sb.Append($" ELSE {Else.Emit(ctx)}");
        sb.Append(" END");
        return sb.ToString();
    }
}
