namespace DBWeaver.Expressions.Operations;

public enum LogicOperator
{
    And,
    Or,
}

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
