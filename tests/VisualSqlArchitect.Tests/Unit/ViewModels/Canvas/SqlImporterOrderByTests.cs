using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

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

        Assert.True(resultNode.Parameters.TryGetValue("import_order_terms", out string? orderTermsRaw));
        Assert.False(string.IsNullOrWhiteSpace(orderTermsRaw));

        string[] terms = orderTermsRaw!.Split(';', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, terms.Length);

        string[] first = terms[0].Split('|');
        string[] second = terms[1].Split('|');

        Assert.Equal(3, first.Length);
        Assert.Equal(3, second.Length);

        Assert.Equal("customer_id", first[1], ignoreCase: true);
        Assert.Equal("ASC", first[2], ignoreCase: true);
        Assert.Equal("id", second[1], ignoreCase: true);
        Assert.Equal("DESC", second[2], ignoreCase: true);
    }
}
