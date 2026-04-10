using DBWeaver.Core;
using DBWeaver.Providers.Dialects;
using DBWeaver.Registry;

namespace DBWeaver.Ddl;

/// <summary>
/// Immutable runtime context used while emitting DDL SQL.
/// </summary>
public sealed class DdlEmitContext
{
    public DatabaseProvider Provider { get; }
    public ISqlDialect Dialect { get; }

    public DdlEmitContext(DatabaseProvider provider, IProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Provider = provider;
        Dialect = registry.GetDialect(provider);
    }

    public DdlEmitContext(DatabaseProvider provider)
        : this(provider, new ProviderRegistry(DefaultProviderRegistrations.CreateAll()))
    {
    }
}
