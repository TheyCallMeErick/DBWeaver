using DBWeaver.Core;
using DBWeaver.Metadata;
using DBWeaver.Metadata.Inspectors;
using Xunit;

namespace DBWeaver.Tests.Unit.Metadata;

public sealed class DatabaseInspectorFactoryTests
{
    [Fact]
    public void Constructor_WithRegistrations_AllowsCustomProviderMap()
    {
        var sut = new DatabaseInspectorFactory(
        [
            new InspectorRegistration(
                DatabaseProvider.Postgres,
                cfg => new FakeInspector(cfg.Provider)
            ),
        ]);

        IDatabaseInspector inspector = sut.Create(BuildConfig(DatabaseProvider.Postgres));
        Assert.IsType<FakeInspector>(inspector);

        Assert.False(sut.IsRegistered(DatabaseProvider.SQLite));
        Assert.Throws<NotSupportedException>(() => sut.Create(BuildConfig(DatabaseProvider.SQLite)));
    }

    [Theory]
    [InlineData(DatabaseProvider.Postgres, typeof(PostgresInspector))]
    [InlineData(DatabaseProvider.MySql, typeof(MySqlInspector))]
    [InlineData(DatabaseProvider.SqlServer, typeof(SqlServerInspector))]
    [InlineData(DatabaseProvider.SQLite, typeof(SqliteInspector))]
    public void Create_DefaultFactory_ReturnsProviderInspector(
        DatabaseProvider provider,
        Type expectedType
    )
    {
        var sut = DatabaseInspectorFactory.CreateDefault();
        IDatabaseInspector inspector = sut.Create(BuildConfig(provider));
        Assert.IsType(expectedType, inspector);
    }

    [Fact]
    public void Register_NewProvider_AllowsCreate()
    {
        var sut = DatabaseInspectorFactory.CreateDefault();
        Assert.True(sut.IsRegistered(DatabaseProvider.SQLite));

        _ = sut.Register(DatabaseProvider.SQLite, cfg => new FakeInspector(cfg.Provider));

        Assert.True(sut.IsRegistered(DatabaseProvider.SQLite));
        IDatabaseInspector inspector = sut.Create(BuildConfig(DatabaseProvider.SQLite));
        Assert.IsType<FakeInspector>(inspector);
        Assert.Equal(DatabaseProvider.SQLite, inspector.Provider);
    }

    [Fact]
    public void Register_ExistingProvider_ReturnsPreviousFactory()
    {
        var sut = DatabaseInspectorFactory.CreateDefault();

        Func<ConnectionConfig, IDatabaseInspector>? previous = sut.Register(
            DatabaseProvider.Postgres,
            cfg => new FakeInspector(cfg.Provider)
        );

        Assert.NotNull(previous);
        IDatabaseInspector restored = previous!(BuildConfig(DatabaseProvider.Postgres));
        Assert.IsType<PostgresInspector>(restored);

        IDatabaseInspector current = sut.Create(BuildConfig(DatabaseProvider.Postgres));
        Assert.IsType<FakeInspector>(current);
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

    private sealed class FakeInspector(DatabaseProvider provider) : IDatabaseInspector
    {
        public DatabaseProvider Provider => provider;

        public Task<DbMetadata> InspectAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<TableMetadata> InspectTableAsync(
            string schema,
            string table,
            CancellationToken ct = default
        ) => throw new NotImplementedException();

        public Task<IReadOnlyList<ForeignKeyRelation>> GetForeignKeysAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
