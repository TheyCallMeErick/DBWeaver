namespace VisualSqlArchitect.Expressions.Columns;

/// <summary>
/// Passes a raw SQL fragment through unchanged (escape hatch for advanced users).
/// </summary>
public sealed record RawSqlExpr(string Sql, PinDataType OutputType = PinDataType.Expression)
    : ISqlExpression
{
    public string Emit(EmitContext ctx) => Sql;
}
