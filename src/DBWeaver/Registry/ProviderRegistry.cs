using System.Collections.Concurrent;
using DBWeaver.Core;
using DBWeaver.Metadata;
using DBWeaver.Providers.Dialects;
using DBWeaver.QueryEngine;

namespace DBWeaver.Registry;

public sealed class ProviderRegistry : IProviderRegistry
{
    private readonly ConcurrentDictionary<DatabaseProvider, ISqlDialect> _dialects = new();
    private readonly ConcurrentDictionary<DatabaseProvider, IMetadataQueryProvider> _metadataProviders = new();
    private readonly ConcurrentDictionary<DatabaseProvider, IFunctionFragmentProvider> _functionFragments = new();
    private readonly ConcurrentDictionary<DatabaseProvider, ISqlFunctionRegistry> _registryCache = new();

    public ProviderRegistry()
    {
    }

    public ProviderRegistry(IEnumerable<IProviderRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        foreach (IProviderRegistration registration in registrations)
        {
            RegisterProvider(
                registration.Provider,
                registration.Dialect,
                registration.MetadataProvider,
                registration.FunctionFragments
            );
        }
    }

    public static IProviderRegistry CreateDefault()
    {
        return new ProviderRegistry(DefaultProviderRegistrations.CreateAll());
    }

    public void RegisterProvider(
        DatabaseProvider provider,
        ISqlDialect dialect,
        IMetadataQueryProvider metadataProvider,
        IFunctionFragmentProvider functionFragments)
    {
        ArgumentNullException.ThrowIfNull(dialect);
        ArgumentNullException.ThrowIfNull(metadataProvider);
        ArgumentNullException.ThrowIfNull(functionFragments);

        _dialects[provider] = dialect;
        _metadataProviders[provider] = metadataProvider;
        _functionFragments[provider] = functionFragments;
        _registryCache.TryRemove(provider, out _);
    }

    public ISqlFunctionRegistry CreateFunctionRegistry(DatabaseProvider provider)
    {
        return _registryCache.GetOrAdd(provider, static p => new SqlFunctionRegistry(p));
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

}
