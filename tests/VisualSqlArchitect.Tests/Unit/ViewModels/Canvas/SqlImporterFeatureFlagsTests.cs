using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

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

        Assert.False(string.IsNullOrWhiteSpace(canvas.SqlImporter.StatusMessage));
        Assert.Contains("line", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("column", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(canvas.SqlImporter.HasReport);
    }
}


