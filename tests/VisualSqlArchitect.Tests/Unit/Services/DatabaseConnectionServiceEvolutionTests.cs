using DBWeaver.Core;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.Services;

public sealed class DatabaseConnectionServiceEvolutionTests
{
    [Fact]
    public async Task ListDatabasesAsync_ReturnsEmpty_WhenNoActiveConnection()
    {
        using var service = new DatabaseConnectionService();

        string[] databases = await service.ListDatabasesAsync();

        Assert.NotNull(databases);
        Assert.Empty(databases);
    }

    [Fact]
    public async Task GetServerVersionAsync_ReturnsNull_WhenNoActiveConnection()
    {
        using var service = new DatabaseConnectionService();

        string? version = await service.GetServerVersionAsync();

        Assert.Null(version);
    }

    [Fact]
    public async Task SQLiteConnection_PopulatesVersion_AndDatabaseList()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"vsa-test-{Guid.NewGuid():N}.db");
        var config = new ConnectionConfig(
            Provider: DatabaseProvider.SQLite,
            Host: string.Empty,
            Port: 0,
            Database: dbPath,
            Username: string.Empty,
            Password: string.Empty);

        try
        {
            using var service = new DatabaseConnectionService();
            var searchMenu = new SearchMenuViewModel();

            await service.ConnectAndLoadAsync(config, searchMenu);

            string? version = await service.GetServerVersionAsync();
            string[] databases = await service.ListDatabasesAsync();

            Assert.False(string.IsNullOrWhiteSpace(version));
            Assert.Single(databases);
            Assert.Equal(Path.GetFileNameWithoutExtension(dbPath), databases[0]);
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task SwitchDatabaseAsync_DoesNothing_WhenNoActiveConnection()
    {
        using var service = new DatabaseConnectionService();

        await service.SwitchDatabaseAsync("any");

        string[] databases = await service.ListDatabasesAsync();
        Assert.Empty(databases);
    }
}
