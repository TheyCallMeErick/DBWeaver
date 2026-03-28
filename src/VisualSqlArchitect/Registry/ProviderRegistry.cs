using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata;
using VisualSqlArchitect.Providers.Dialects;
using VisualSqlArchitect.QueryEngine;

namespace VisualSqlArchitect.Registry;

public sealed class ProviderRegistry : IProviderRegistry
{
    private readonly Dictionary<DatabaseProvider, ISqlDialect> _dialects = new();
    private readonly Dictionary<DatabaseProvider, IMetadataQueryProvider> _metadataProviders = new();
    private readonly Dictionary<DatabaseProvider, IFunctionFragmentProvider> _functionFragments = new();
    private readonly Dictionary<DatabaseProvider, ISqlFunctionRegistry> _registryCache = new();

    public static IProviderRegistry CreateDefault()
    {
        var registry = new ProviderRegistry();
        registry.RegisterDefaultProviders();
        return registry;
    }

    public void RegisterProvider(
        DatabaseProvider provider,
        ISqlDialect dialect,
        IMetadataQueryProvider metadataProvider,
        IFunctionFragmentProvider functionFragments)
    {
        if (dialect == null) throw new ArgumentNullException(nameof(dialect));
        if (metadataProvider == null) throw new ArgumentNullException(nameof(metadataProvider));
        if (functionFragments == null) throw new ArgumentNullException(nameof(functionFragments));

        _dialects[provider] = dialect;
        _metadataProviders[provider] = metadataProvider;
        _functionFragments[provider] = functionFragments;
        _registryCache.Remove(provider);
    }

    public ISqlFunctionRegistry CreateFunctionRegistry(DatabaseProvider provider)
    {
        if (!_registryCache.TryGetValue(provider, out ISqlFunctionRegistry? cached))
        {
            cached = new SqlFunctionRegistry(provider);
            _registryCache[provider] = cached;
        }
        return cached;
    }

    public QueryBuilderService CreateQueryBuilder(DatabaseProvider provider, string fromTable)
    {
        if (string.IsNullOrEmpty(fromTable)) throw new ArgumentNullException(nameof(fromTable));
        var registry = CreateFunctionRegistry(provider);
        return QueryBuilderService.Create(provider, fromTable, registry);
    }

    public ISqlDialect GetDialect(DatabaseProvider provider)
    {
        if (!_dialects.TryGetValue(provider, out ISqlDialect? dialect)) 
            throw new NotSupportedException("Provider " + provider.ToString() + " is not registered.");
        return dialect;
    }

    public IMetadataQueryProvider GetMetadataProvider(DatabaseProvider provider)
    {
        if (!_metadataProviders.TryGetValue(provider, out IMetadataQueryProvider? metadata))
            throw new NotSupportedException("Provider " + provider.ToString() + " is not registered.");
        return metadata;
    }

    public IFunctionFragmentProvider GetFunctionFragments(DatabaseProvider provider)
    {
        if (!_functionFragments.TryGetValue(provider, out IFunctionFragmentProvider? fragments))
            throw new NotSupportedException("Provider " + provider.ToString() + " is not registered.");
        return fragments;
    }

    public bool IsProviderRegistered(DatabaseProvider provider) =>
        _dialects.ContainsKey(provider) && _metadataProviders.ContainsKey(provider) && _functionFragments.ContainsKey(provider);

    public IReadOnlyList<DatabaseProvider> GetRegisteredProviders() =>
        _dialects.Keys.ToList().AsReadOnly();

    private void RegisterDefaultProviders()
    {
        RegisterProvider(DatabaseProvider.Postgres, new PostgresDialect(), new PostgresMetadataQueries(), new PostgresFunctionFragments());
        RegisterProvider(DatabaseProvider.MySql, new MySqlDialect(), new MySqlMetadataQueries(), new MySqlFunctionFragments());
        RegisterProvider(DatabaseProvider.SqlServer, new SqlServerDialect(), new SqlServerMetadataQueries(), new SqlServerFunctionFragments());
    }
}
