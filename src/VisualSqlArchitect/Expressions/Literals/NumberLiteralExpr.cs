namespace VisualSqlArchitect.Expressions.Literals;

/// <summary>A numeric constant: 3.14, -7, 0</summary>
public sealed record NumberLiteralExpr(double Value) : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Number;

    public string Emit(EmitContext ctx) =>
        Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
