namespace VisualSqlArchitect.Expressions.Functions;

/// <summary>
/// Calls a canonical function through the <see cref="ISqlFunctionRegistry"/>.
/// Each child expression is emitted first; the resulting strings are passed
/// as args to the registry.
///
/// Example: FunctionCallExpr(SqlFn.Upper, [ColumnExpr("users","email")])
///   Postgres/MySQL/SQL Server → UPPER("users"."email")
/// </summary>
public sealed record FunctionCallExpr(
    string FunctionName,
    IReadOnlyList<ISqlExpression> Args,
    PinDataType OutputType = PinDataType.Any
) : ISqlExpression
{
    public string Emit(EmitContext ctx)
    {
        string[] emittedArgs = [.. Args.Select(a => a.Emit(ctx))];
        return ctx.Registry.GetFunction(FunctionName, emittedArgs);
    }
}
