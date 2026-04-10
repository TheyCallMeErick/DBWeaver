namespace DBWeaver.Expressions.Literals;

/// <summary>A raw string literal: 'hello', 42, true, NULL</summary>
public sealed record LiteralExpr(string RawValue, PinDataType OutputType = PinDataType.Expression)
    : ISqlExpression
{
    public string Emit(EmitContext ctx) => RawValue;
}
