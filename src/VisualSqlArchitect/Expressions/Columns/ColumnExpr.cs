namespace VisualSqlArchitect.Expressions.Columns;

/// <summary>
/// A table column reference: table.column — the "output pin" of a DataSource node.
/// Every column on the canvas becomes one of these.
/// </summary>
public sealed record ColumnExpr(
    string TableAlias,
    string ColumnName,
    PinDataType OutputType = PinDataType.Any
) : ISqlExpression
{
    public string Emit(EmitContext ctx) =>
        string.IsNullOrEmpty(TableAlias)
            ? ctx.QuoteIdentifier(ColumnName)
            : $"{ctx.QuoteIdentifier(TableAlias)}.{ctx.QuoteIdentifier(ColumnName)}";
}
