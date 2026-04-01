using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class SqlImporterWiringTests
{
    [Fact]
    public async Task ImportAsync_WithJoinAndSimpleWhere_CreatesJoinNodeAndWiresWhereToResult()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT orders.id FROM orders INNER JOIN customers ON orders.customer_id = customers.id WHERE orders.id = 1";

        await canvas.SqlImporter.ImportAsync();

        NodeViewModel joinNode = canvas.Nodes.First(n => n.Type == NodeType.Join);
        Assert.Equal("INNER", joinNode.Parameters["join_type"]);
        Assert.Equal("customers", joinNode.Parameters["right_source"]);

        NodeViewModel whereNode = canvas.Nodes.First(n => n.Type == NodeType.WhereOutput);
        NodeViewModel resultNode = canvas.Nodes.First(n => n.Type == NodeType.ResultOutput);

        Assert.Contains(canvas.Connections, c =>
            c.FromPin.Owner == whereNode
            && c.FromPin.Name == "result"
            && c.ToPin?.Owner == resultNode
            && c.ToPin.Name == "where");
    }
}
