namespace VisualSqlArchitect.Expressions.Literals;

/// <summary>NULL sentinel.</summary>
public sealed record NullExpr : ISqlExpression
{
    public static readonly NullExpr Instance = new();
    public PinDataType OutputType => PinDataType.Any;

    public string Emit(EmitContext ctx) => "NULL";
}
