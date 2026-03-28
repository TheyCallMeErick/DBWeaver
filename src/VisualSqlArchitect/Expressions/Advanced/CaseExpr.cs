namespace VisualSqlArchitect.Expressions.Advanced;

/// <summary>WHEN condition THEN result clause.</summary>
public sealed record WhenClause(ISqlExpression Condition, ISqlExpression Result);

/// <summary>
/// CASE WHEN ... THEN ... ELSE ... END statement.
/// </summary>
public sealed record CaseExpr(IReadOnlyList<WhenClause> Whens, ISqlExpression? Else = null)
    : ISqlExpression
{
    public PinDataType OutputType => Else?.OutputType ?? PinDataType.Any;

    public string Emit(EmitContext ctx)
    {
        var sb = new System.Text.StringBuilder("CASE");
        foreach (WhenClause w in Whens)
            sb.Append($" WHEN {w.Condition.Emit(ctx)} THEN {w.Result.Emit(ctx)}");
        if (Else is not null)
            sb.Append($" ELSE {Else.Emit(ctx)}");
        sb.Append(" END");
        return sb.ToString();
    }
}
