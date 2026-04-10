namespace DBWeaver.Expressions.Functions;

public enum AggregateFunction
{
    Count,
    Sum,
    Avg,
    Min,
    Max,
}

/// <summary>
/// Standard SQL aggregate functions: COUNT, SUM, AVG, MIN, MAX.
/// </summary>
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
