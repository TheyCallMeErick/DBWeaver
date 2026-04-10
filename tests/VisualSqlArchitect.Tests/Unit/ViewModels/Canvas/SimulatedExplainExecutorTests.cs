using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.Core;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class SimulatedExplainExecutorTests
{
    [Theory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SqlServer)]
    [InlineData(DatabaseProvider.SQLite)]
    public async Task RunAsync_ReturnsSimulatedResult_ForAnyProvider(DatabaseProvider provider)
    {
        var sut = new SimulatedExplainExecutor();

        ExplainResult result = await sut.RunAsync(
            "SELECT * FROM orders ORDER BY id",
            provider,
            connectionConfig: null,
            new ExplainOptions()
        );

        Assert.True(result.IsSimulated);
        Assert.NotEmpty(result.Nodes);
    }

    [Fact]
    public async Task RunAsync_PostgresSimulation_EmitsSortAndPotentialAlerts()
    {
        var sut = new SimulatedExplainExecutor();

        ExplainResult result = await sut.RunAsync(
            "SELECT * FROM orders LIMIT 10 ORDER BY placed_at",
            DatabaseProvider.Postgres,
            connectionConfig: null,
            new ExplainOptions()
        );

        Assert.Contains(result.Nodes, n => n.NodeType == "Sort");
        Assert.Contains(result.Nodes, n => n.AlertLabel == "SORT");
    }

    [Fact]
    public async Task RunAsync_MySqlSimulation_EmitsTableNodes()
    {
        var sut = new SimulatedExplainExecutor();

        ExplainResult result = await sut.RunAsync(
            "SELECT * FROM orders JOIN customers ON orders.id = customers.id",
            DatabaseProvider.MySql,
            connectionConfig: null,
            new ExplainOptions()
        );

        Assert.All(result.Nodes, n => Assert.False(string.IsNullOrWhiteSpace(n.NodeType)));
    }

    [Fact]
    public async Task RunAsync_PostgresSimulation_WithJoin_EmitsHashJoinNode()
    {
        var sut = new SimulatedExplainExecutor();

        ExplainResult result = await sut.RunAsync(
            "SELECT * FROM orders JOIN customers ON orders.id = customers.id WHERE orders.id > 0",
            DatabaseProvider.Postgres,
            connectionConfig: null,
            new ExplainOptions()
        );

        Assert.Contains(result.Nodes, n => n.NodeType == "Hash Join");
    }

    [Fact]
    public async Task RunAsync_SqlServerSimulation_WithJoinAndOrder_EmitsTopSortAndLoop()
    {
        var sut = new SimulatedExplainExecutor();

        ExplainResult result = await sut.RunAsync(
            "SELECT TOP 10 * FROM orders JOIN customers ON orders.id = customers.id ORDER BY orders.id",
            DatabaseProvider.SqlServer,
            connectionConfig: null,
            new ExplainOptions()
        );

        Assert.Contains(result.Nodes, n => n.NodeType == "Top");
        Assert.Contains(result.Nodes, n => n.NodeType == "Sort");
        Assert.Contains(result.Nodes, n => n.NodeType == "Nested Loops");
    }

    [Fact]
    public async Task RunAsync_Throws_WhenSqlIsEmpty()
    {
        var sut = new SimulatedExplainExecutor();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.RunAsync("", DatabaseProvider.Postgres, connectionConfig: null, new ExplainOptions())
        );
    }
}


