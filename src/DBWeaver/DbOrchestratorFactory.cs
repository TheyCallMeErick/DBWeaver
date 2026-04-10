using DBWeaver.Core;
using DBWeaver.Providers;

namespace DBWeaver;

public interface IDbOrchestratorFactory
{
    IDbOrchestrator Create(ConnectionConfig config);
    Func<ConnectionConfig, IDbOrchestrator>? Register(
        DatabaseProvider provider,
        Func<ConnectionConfig, IDbOrchestrator> factory
    );
    bool IsRegistered(DatabaseProvider provider);
}

public sealed record OrchestratorRegistration(
    DatabaseProvider Provider,
    Func<ConnectionConfig, IDbOrchestrator> Factory
);

/// <summary>
/// Single creation point for <see cref="IDbOrchestrator"/> instances.
/// </summary>
public sealed class DbOrchestratorFactory : IDbOrchestratorFactory
{
    private readonly ProviderFactoryRegistry<IDbOrchestrator> _registry;

    public DbOrchestratorFactory(IEnumerable<OrchestratorRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        _registry = new ProviderFactoryRegistry<IDbOrchestrator>(
            registrations.Select(item =>
                new KeyValuePair<DatabaseProvider, Func<ConnectionConfig, IDbOrchestrator>>(
                    item.Provider,
                    item.Factory
                )
            ),
            unsupportedMessageFactory: provider => $"Provider '{provider}' is not supported."
        );
    }

    public static DbOrchestratorFactory CreateDefault() =>
        new(CreateDefaultRegistrations());

    internal static IReadOnlyList<OrchestratorRegistration> CreateDefaultRegistrations() =>
    [
        new(DatabaseProvider.SqlServer, config => new SqlServerOrchestrator(config)),
        new(DatabaseProvider.MySql, config => new MySqlOrchestrator(config)),
        new(DatabaseProvider.Postgres, config => new PostgresOrchestrator(config)),
        new(DatabaseProvider.SQLite, config => new SqliteOrchestrator(config)),
    ];

    public IDbOrchestrator Create(ConnectionConfig config) => _registry.Create(config);

    public Func<ConnectionConfig, IDbOrchestrator>? Register(
        DatabaseProvider provider,
        Func<ConnectionConfig, IDbOrchestrator> factory
    ) => _registry.Register(provider, factory);

    public bool IsRegistered(DatabaseProvider provider) => _registry.IsRegistered(provider);
}
