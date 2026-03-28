namespace VisualSqlArchitect.Expressions.Operations;

public enum ComparisonOperator
{
    Eq,
    Neq,
    Gt,
    Gte,
    Lt,
    Lte,
    Like,
    NotLike,
}

/// <summary>Standard binary comparison: left OP right</summary>
public sealed record ComparisonExpr(
    ISqlExpression Left,
    ComparisonOperator Op,
    ISqlExpression Right
) : ISqlExpression
{
    public PinDataType OutputType => PinDataType.Boolean;

    public string Emit(EmitContext ctx)
    {
        string l = Left.Emit(ctx);
        string r = Right.Emit(ctx);
        string op = Op switch
        {
            ComparisonOperator.Eq => "=",
            ComparisonOperator.Neq => "<>",
            ComparisonOperator.Gt => ">",
            ComparisonOperator.Gte => ">=",
            ComparisonOperator.Lt => "<",
            ComparisonOperator.Lte => "<=",
            ComparisonOperator.Like => "LIKE",
            ComparisonOperator.NotLike => "NOT LIKE",
            _ => throw new NotSupportedException($"Unknown operator: {Op}"),
        };
        return $"({l} {op} {r})";
    }
}
