namespace DBWeaver.Expressions.Columns;

/// <summary>
/// Wraps any expression with AS alias.
/// </summary>
public sealed record AliasExpr(ISqlExpression Inner, string Alias) : ISqlExpression
{
    public PinDataType OutputType => Inner.OutputType;

    public string Emit(EmitContext ctx) => $"{Inner.Emit(ctx)} AS {ctx.QuoteIdentifier(Alias)}";
}
