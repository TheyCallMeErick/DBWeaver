namespace DBWeaver.Expressions.Operations;

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
