

namespace DBWeaver.Unit.Compilation;

/// <summary>
/// Tests for DdlGraphCompilerAdapter - verifies wrapping behavior and error handling.
/// </summary>
public class DdlGraphCompilerAdapterTests
{
    [Fact]
    public void TryCompile_WithNullGraph_ReturnsFalse()
    {
        // Arrange
        var adapter = new DdlGraphCompilerAdapter(DatabaseProvider.Postgres);

        // Act
        bool result = adapter.TryCompile(null!, out var output, out var errors);

        // Assert
        Assert.False(result);
        Assert.Null(output);
        Assert.NotEmpty(errors);
    }

    [Theory]
    [InlineData(DatabaseProvider.SqlServer)]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SQLite)]
    public void TryCompile_WithEmptyGraph_ReturnsConsistentBehavior(DatabaseProvider provider)
    {
        // Arrange
        var graph = new NodeGraph { Nodes = [], Connections = [] };
        var adapter = new DdlGraphCompilerAdapter(provider);

        // Act
        bool result = adapter.TryCompile(graph, out var output, out var errors);

        // Assert
        Assert.True(result);
        Assert.NotNull(output);
        Assert.Empty(errors);
    }

    [Fact]
    public void TryCompile_ReturnsErrorsFromCompiler()
    {
        // Arrange - minimal invalid graph (only output node, no table)
        var nodes = new List<NodeInstance>
        {
            new("output-1", NodeType.CreateTableOutput, new Dictionary<string, string>(), new Dictionary<string, string>())
        };
        var graph = new NodeGraph { Nodes = nodes, Connections = [] };
        var adapter = new DdlGraphCompilerAdapter(DatabaseProvider.Postgres);

        // Act
        bool result = adapter.TryCompile(graph, out var output, out var errors);

        // Assert
        Assert.False(result, "Should return false for invalid graph");
        Assert.Null(output);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void TryCompile_WithValidProvider_ExecutesWithoutException()
    {
        // Arrange
        var graph = new NodeGraph { Nodes = [], Connections = [] };
        var adapter = new DdlGraphCompilerAdapter(DatabaseProvider.SqlServer);

        // Act & Assert - should not throw and should succeed for an empty graph.
        bool result = adapter.TryCompile(graph, out var output, out var errors);

        Assert.True(result);
        Assert.NotNull(output);
        Assert.Empty(errors);
    }
}
