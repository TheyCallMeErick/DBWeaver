using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class SqlImporterParseErrorPositionTests
{
    [Fact]
    public async Task ImportAsync_WithUnmatchedParenthesis_ShowsSyntaxPosition()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT id FROM orders WHERE (id = 1";

        await canvas.SqlImporter.ImportAsync();

        Assert.Contains("Parse error", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("line", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("column", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportAsync_WithUnterminatedString_ShowsSyntaxPosition()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT id FROM orders WHERE note = 'abc";

        await canvas.SqlImporter.ImportAsync();

        Assert.Contains("Parse error", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("line", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("column", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unterminated", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }
}
