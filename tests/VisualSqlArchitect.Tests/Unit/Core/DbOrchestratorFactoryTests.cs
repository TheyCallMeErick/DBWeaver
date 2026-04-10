using DBWeaver;
using DBWeaver.Core;

namespace DBWeaver.Tests.Unit.Core;

public class DbOrchestratorFactoryTests
{
    [Theory]
    [InlineData(DatabaseProvider.SqlServer, typeof(DBWeaver.Providers.SqlServerOrchestrator))]
    [InlineData(DatabaseProvider.MySql, typeof(DBWeaver.Providers.MySqlOrchestrator))]
    [InlineData(DatabaseProvider.Postgres, typeof(DBWeaver.Providers.PostgresOrchestrator))]
    [InlineData(DatabaseProvider.SQLite, typeof(DBWeaver.Providers.SqliteOrchestrator))]
    public void Create_ReturnsRegisteredDefaultOrchestrator(DatabaseProvider provider, Type expectedType)
    {
        IDbOrchestratorFactory factory = DbOrchestratorFactory.CreateDefault();
        var config = BuildConfig(provider);

        IDbOrchestrator orchestrator = factory.Create(config);

        Assert.IsType(expectedType, orchestrator);
    }

    [Fact]
    public void Register_AllowsOverridingFactory_AndCanRestorePrevious()
    {
        IDbOrchestratorFactory factory = DbOrchestratorFactory.CreateDefault();
        var concrete = Assert.IsType<DbOrchestratorFactory>(factory);
        var config = BuildConfig(DatabaseProvider.SQLite);

        Func<ConnectionConfig, IDbOrchestrator>? previous = concrete.Register(
            DatabaseProvider.SQLite,
            cfg => new FakeOrchestrator(cfg)
        );

        Assert.NotNull(previous);
        IDbOrchestrator orchestrator = concrete.Create(config);
        Assert.IsType<FakeOrchestrator>(orchestrator);

        _ = concrete.Register(DatabaseProvider.SQLite, previous!);
        IDbOrchestrator restored = concrete.Create(config);
        Assert.IsNotType<FakeOrchestrator>(restored);
    }

    [Fact]
    public void IsRegistered_AfterRegister_ReturnsTrue()
    {
        var concrete = DbOrchestratorFactory.CreateDefault();
        Assert.True(concrete.IsRegistered(DatabaseProvider.Postgres));
    }

    private static ConnectionConfig BuildConfig(DatabaseProvider provider) =>
        new(
            Provider: provider,
            Host: "localhost",
            Port: provider == DatabaseProvider.SQLite ? 0 : 5432,
            Database: provider == DatabaseProvider.SQLite ? "test.db" : "db",
            Username: "user",
            Password: "pass"
        );

    private sealed class FakeOrchestrator(ConnectionConfig config) : IDbOrchestrator
    {
        public DatabaseProvider Provider => config.Provider;
        public ConnectionConfig Config => config;

        public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default) =>
            Task.FromResult(new ConnectionTestResult(true));

        public Task<DatabaseSchema> GetSchemaAsync(CancellationToken ct = default) =>
            Task.FromResult(new DatabaseSchema("fake", config.Provider, []));

        public Task<PreviewResult> ExecutePreviewAsync(
            string sql,
            int maxRows = PreviewExecutionOptions.UseConfiguredDefault,
            CancellationToken ct = default
        ) => Task.FromResult(new PreviewResult(true));

        public Task<DdlExecutionResult> ExecuteDdlAsync(
            string sql,
            bool stopOnError = true,
            CancellationToken ct = default
        ) => Task.FromResult(new DdlExecutionResult(true, []));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
