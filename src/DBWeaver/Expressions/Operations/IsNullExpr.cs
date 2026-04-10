namespace DBWeaver.Expressions.Operations;

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
