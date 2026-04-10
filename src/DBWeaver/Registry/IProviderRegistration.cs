using DBWeaver.Core;
using DBWeaver.Metadata;
using DBWeaver.Providers.Dialects;
using DBWeaver.QueryEngine;

namespace DBWeaver.Registry;

public interface IProviderRegistration
{
    DatabaseProvider Provider { get; }
    ISqlDialect Dialect { get; }
    IMetadataQueryProvider MetadataProvider { get; }
    IFunctionFragmentProvider FunctionFragments { get; }
}

public sealed record ProviderRegistration(
    DatabaseProvider Provider,
    ISqlDialect Dialect,
    IMetadataQueryProvider MetadataProvider,
    IFunctionFragmentProvider FunctionFragments
) : IProviderRegistration;

internal static class DefaultProviderRegistrations
{
    public static IReadOnlyList<IProviderRegistration> CreateAll() =>
    [
        new ProviderRegistration(
            DatabaseProvider.Postgres,
            new PostgresDialect(),
            new PostgresMetadataQueries(),
            new PostgresFunctionFragments()
        ),
        new ProviderRegistration(
            DatabaseProvider.MySql,
            new MySqlDialect(),
            new MySqlMetadataQueries(),
            new MySqlFunctionFragments()
        ),
        new ProviderRegistration(
            DatabaseProvider.SqlServer,
            new SqlServerDialect(),
            new SqlServerMetadataQueries(),
            new SqlServerFunctionFragments()
        ),
        new ProviderRegistration(
            DatabaseProvider.SQLite,
            new SqliteDialect(),
            new SqliteMetadataQueries(),
            new SqliteFunctionFragments()
        ),
    ];
}
