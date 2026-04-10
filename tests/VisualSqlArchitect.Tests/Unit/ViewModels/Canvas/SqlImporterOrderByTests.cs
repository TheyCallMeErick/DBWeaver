using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class SqlImporterOrderByTests
{
    [Fact]
    public async Task ImportAsync_WithMultipleOrderByTerms_PreservesTermOrderAndDirectionMetadata()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT id, customer_id FROM orders ORDER BY customer_id ASC, id DESC";

        await canvas.SqlImporter.ImportAsync();

        var resultNode = canvas.Nodes.First(n => n.Type == NodeType.ResultOutput);
        var orderConnections = canvas.Connections
            .Where(c => c.ToPin?.Owner == resultNode
                && (c.ToPin.Name.Equals("order_by", StringComparison.OrdinalIgnoreCase)
                    || c.ToPin.Name.Equals("order_by_desc", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Equal(2, orderConnections.Count);
        Assert.Contains(orderConnections, c =>
            c.ToPin!.Name.Equals("order_by", StringComparison.OrdinalIgnoreCase)
            && c.FromPin.Name.Equals("customer_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(orderConnections, c =>
            c.ToPin!.Name.Equals("order_by_desc", StringComparison.OrdinalIgnoreCase)
            && c.FromPin.Name.Equals("id", StringComparison.OrdinalIgnoreCase));
    }
}

