namespace DBWeaver.Core;

internal sealed class ProviderFactoryRegistry<TService>
{
    private readonly Lock _gate = new();
    private readonly Dictionary<DatabaseProvider, Func<ConnectionConfig, TService>> _factories;
    private readonly Func<DatabaseProvider, string> _unsupportedMessageFactory;

    public ProviderFactoryRegistry(
        IEnumerable<KeyValuePair<DatabaseProvider, Func<ConnectionConfig, TService>>> registrations,
        Func<DatabaseProvider, string> unsupportedMessageFactory
    )
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(unsupportedMessageFactory);

        _unsupportedMessageFactory = unsupportedMessageFactory;
        _factories = registrations.ToDictionary(item => item.Key, item => item.Value);
    }

    public TService Create(ConnectionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Func<ConnectionConfig, TService>? factory;
        lock (_gate)
        {
            if (!_factories.TryGetValue(config.Provider, out factory))
                throw new NotSupportedException(_unsupportedMessageFactory(config.Provider));
        }

        return factory(config);
    }

    public Func<ConnectionConfig, TService>? Register(
        DatabaseProvider provider,
        Func<ConnectionConfig, TService> factory
    )
    {
        ArgumentNullException.ThrowIfNull(factory);

        lock (_gate)
        {
            _factories.TryGetValue(provider, out Func<ConnectionConfig, TService>? previous);
            _factories[provider] = factory;
            return previous;
        }
    }

    public bool IsRegistered(DatabaseProvider provider)
    {
        lock (_gate)
            return _factories.ContainsKey(provider);
    }
}
