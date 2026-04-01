using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class SqlImporterFeatureFlagsTests
{
    [Fact]
    public async Task ImportAsync_WhenAstFlagDisabled_UsesLegacyParserSuccessfully()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.FeatureFlags.UseAstParser = false;
        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT id FROM orders";

        await canvas.SqlImporter.ImportAsync();

        Assert.Contains("Done", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(canvas.SqlImporter.HasReport);
    }

    [Fact]
    public async Task ImportAsync_WhenAstFlagEnabled_UsesAstPathSuccessfullyForValidSql()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.FeatureFlags.UseAstParser = true;
        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT id FROM orders";

        await canvas.SqlImporter.ImportAsync();

        Assert.Contains("Done", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(canvas.SqlImporter.HasReport);
    }

    [Fact]
    public async Task ImportAsync_WhenAstFlagEnabledAndSyntaxInvalid_ShowsLineAndColumn()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.FeatureFlags.UseAstParser = true;
        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT id FROM orders WHERE (id = 1";

        await canvas.SqlImporter.ImportAsync();

        Assert.Contains("Parse error", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("line", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("column", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(canvas.SqlImporter.HasReport);
    }
}
