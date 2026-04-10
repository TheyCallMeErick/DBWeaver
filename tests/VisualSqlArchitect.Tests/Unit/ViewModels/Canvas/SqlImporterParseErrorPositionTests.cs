using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels;
using Xunit;
using System.Text.RegularExpressions;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

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

        Assert.False(string.IsNullOrWhiteSpace(canvas.SqlImporter.StatusMessage));
        Assert.True(HasPositionInfo(canvas.SqlImporter.StatusMessage));
    }

    [Fact]
    public async Task ImportAsync_WithUnterminatedString_ShowsSyntaxPosition()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT id FROM orders WHERE note = 'abc";

        await canvas.SqlImporter.ImportAsync();

        Assert.False(string.IsNullOrWhiteSpace(canvas.SqlImporter.StatusMessage));
        Assert.True(HasPositionInfo(canvas.SqlImporter.StatusMessage));
    }

    private static bool HasPositionInfo(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        string normalized = message.ToLowerInvariant();
        bool hasNamedPosition =
            normalized.Contains("line")
            || normalized.Contains("column")
            || normalized.Contains("linha")
            || normalized.Contains("coluna")
            || normalized.Contains("columna")
            || normalized.Contains("position")
            || normalized.Contains("posicao")
            || normalized.Contains("posição");

        bool hasNumericPair = Regex.IsMatch(message, @"\d+\s*[,;:]\s*\d+")
            || Regex.IsMatch(message, @"\(\s*\d+\s*,\s*\d+\s*\)");

        return hasNamedPosition || hasNumericPair;
    }
}


