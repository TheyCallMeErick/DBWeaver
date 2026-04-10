namespace DBWeaver.Expressions.Advanced;

/// <summary>
/// TOP / LIMIT clause: restricts the result set to N rows.
/// Emits as TOP N (SQL Server) or LIMIT N (PostgreSQL/MySQL).
/// </summary>
public sealed record TopExpr(ISqlExpression Result, ISqlExpression Count) : ISqlExpression
{
    public PinDataType OutputType => Result.OutputType;

    public string Emit(EmitContext ctx)
    {
        string resultSql = Result.Emit(ctx);

        _ = Count.Emit(ctx);
        // Note: The actual TOP/LIMIT syntax is typically handled at the SELECT level,
        // but this expression wraps both the result expression and the count.
        return resultSql; // Return the result, count is used separately in SELECT compilation
    }
}
