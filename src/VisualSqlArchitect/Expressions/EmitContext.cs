using VisualSqlArchitect.Core;
using VisualSqlArchitect.Registry;

namespace VisualSqlArchitect.Expressions;

/// <summary>
/// Passed through every expression during compilation.
/// Carries the provider dialect and the function registry so expressions
/// can produce correct SQL without knowing the database themselves.
/// </summary>
public sealed class EmitContext(DatabaseProvider provider, ISqlFunctionRegistry registry)
{
    public DatabaseProvider Provider { get; } = provider;
    public ISqlFunctionRegistry Registry { get; } = registry;

    public string QuoteIdentifier(string id) =>
        Provider switch
        {
            DatabaseProvider.SqlServer => $"[{id}]",
            DatabaseProvider.MySql => $"`{id}`",
            DatabaseProvider.Postgres => $"\"{id}\"",
            _ => id,
        };

    public static string QuoteLiteral(string value) => $"'{value.Replace("'", "''")}'";
}
