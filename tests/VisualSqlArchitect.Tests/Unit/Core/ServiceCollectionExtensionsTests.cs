using Microsoft.Extensions.DependencyInjection;
using DBWeaver;
using DBWeaver.Core;
using DBWeaver.Metadata;
using DBWeaver.QueryEngine;
using DBWeaver.Registry;
using Xunit;

namespace DBWeaver.Tests.Unit.Core;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddDBWeaver_Default_RegistersCoreServices()
    {
        var services = new ServiceCollection();
        services.AddDBWeaver();

        await using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IProviderRegistry>());
        Assert.NotNull(provider.GetService<IDbOrchestratorFactory>());
        Assert.NotNull(provider.GetService<IDatabaseInspectorFactory>());
        Assert.NotNull(provider.GetService<ICanvasTableTracker>());
        Assert.NotNull(provider.GetService<ActiveConnectionContext>());
        Assert.NotNull(provider.GetService<ISqlFunctionRegistry>());
        Assert.NotNull(provider.GetService<QueryBuilderService>());
    }

    [Fact]
    public async Task AddDBWeaver_AllowsOverridingCanvasTableTrackerFactory()
    {
        var services = new ServiceCollection();
        services.AddDBWeaver(options =>
        {
            options.CanvasTableTrackerFactory = () => new StubCanvasTableTracker();
        });

        await using ServiceProvider provider = services.BuildServiceProvider();
        ICanvasTableTracker tracker = provider.GetRequiredService<ICanvasTableTracker>();

        Assert.IsType<StubCanvasTableTracker>(tracker);
    }

    [Fact]
    public async Task AddDBWeaver_AllowsOverridingInspectorRegistrations()
    {
        var services = new ServiceCollection();
        services.AddDBWeaver(options =>
        {
            options.InspectorRegistrations =
            [
                new InspectorRegistration(
                    DatabaseProvider.Postgres,
                    cfg => new StubInspector(cfg.Provider)
                ),
            ];
        });

        await using ServiceProvider provider = services.BuildServiceProvider();
        IDatabaseInspectorFactory factory = provider.GetRequiredService<IDatabaseInspectorFactory>();

        IDatabaseInspector inspector = factory.Create(BuildConfig(DatabaseProvider.Postgres));
        Assert.IsType<StubInspector>(inspector);
    }

    private static ConnectionConfig BuildConfig(DatabaseProvider provider) =>
        new(
            provider,
            Host: "localhost",
            Port: 5432,
            Database: "db",
            Username: "user",
            Password: "pwd"
        );

    private sealed class StubCanvasTableTracker : ICanvasTableTracker
    {
        private readonly List<string> _tables = [];

        public void Add(string fullTableName) => _tables.Add(fullTableName);
        public void Remove(string fullTableName) => _tables.Remove(fullTableName);
        public bool Contains(string fullTableName) => _tables.Contains(fullTableName, StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<string> Snapshot() => _tables.ToList();
        public int Count => _tables.Count;
    }

    private sealed class StubInspector(DatabaseProvider provider) : IDatabaseInspector
    {
        public DatabaseProvider Provider => provider;

        public Task<DbMetadata> InspectAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<TableMetadata> InspectTableAsync(string schema, string table, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<ForeignKeyRelation>> GetForeignKeysAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
