namespace DBWeaver.Expressions.Operations;

/// <summary>NOT — single operand.</summary>
public sealed record NotExpr(ISqlExpression Operand) : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Boolean;

    public string Emit(EmitContext ctx) => $"(NOT {Operand.Emit(ctx)})";
}
