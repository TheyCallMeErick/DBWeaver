using DBWeaver.Core;
using DBWeaver.Metadata.Inspectors;

namespace DBWeaver.Metadata;

public interface IDatabaseInspectorFactory
{
    IDatabaseInspector Create(ConnectionConfig config);
}

public sealed record InspectorRegistration(
    DatabaseProvider Provider,
    Func<ConnectionConfig, IDatabaseInspector> Factory
);

public sealed class DatabaseInspectorFactory : IDatabaseInspectorFactory
{
    private readonly ProviderFactoryRegistry<IDatabaseInspector> _registry;

    public DatabaseInspectorFactory(IEnumerable<InspectorRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        _registry = new ProviderFactoryRegistry<IDatabaseInspector>(
            registrations.Select(item =>
                new KeyValuePair<DatabaseProvider, Func<ConnectionConfig, IDatabaseInspector>>(
                    item.Provider,
                    item.Factory
                )
            ),
            unsupportedMessageFactory: provider => $"No inspector for '{provider}'."
        );
    }

    public static DatabaseInspectorFactory CreateDefault() =>
        new(CreateDefaultRegistrations());

    internal static IReadOnlyList<InspectorRegistration> CreateDefaultRegistrations() =>
    [
        new(DatabaseProvider.SqlServer, config => new SqlServerInspector(config)),
        new(DatabaseProvider.MySql, config => new MySqlInspector(config)),
        new(DatabaseProvider.Postgres, config => new PostgresInspector(config)),
        new(DatabaseProvider.SQLite, config => new SqliteInspector(config)),
    ];

    public IDatabaseInspector Create(ConnectionConfig config) => _registry.Create(config);

    public Func<ConnectionConfig, IDatabaseInspector>? Register(
        DatabaseProvider provider,
        Func<ConnectionConfig, IDatabaseInspector> factory
    ) => _registry.Register(provider, factory);

    public bool IsRegistered(DatabaseProvider provider) => _registry.IsRegistered(provider);
}
