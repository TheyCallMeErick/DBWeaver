using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Metadata;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class CanvasAutoJoinSuggestionServiceTests
{
    [Fact]
    public void AnalyzeNewTable_ReturnsSuggestions_WhenFkLikeColumnsExist()
    {
        var service = new CanvasAutoJoinSuggestionService();
        var nodes = new List<NodeViewModel>
        {
            CreateTable("public.orders", ("id", PinDataType.Number), ("customer_id", PinDataType.Number)),
            CreateTable("public.customers", ("id", PinDataType.Number)),
        };

        IReadOnlyList<JoinSuggestion> suggestions = service.AnalyzeNewTable("public.orders", nodes);

        Assert.NotEmpty(suggestions);
    }

    [Fact]
    public void AnalyzeAllTables_ReturnsDistinctSuggestionsByPairKey()
    {
        var service = new CanvasAutoJoinSuggestionService();
        var nodes = new List<NodeViewModel>
        {
            CreateTable("public.orders", ("id", PinDataType.Number), ("customer_id", PinDataType.Number)),
            CreateTable("public.customers", ("id", PinDataType.Number)),
            CreateTable("public.payments", ("id", PinDataType.Number), ("order_id", PinDataType.Number)),
        };

        IReadOnlyList<JoinSuggestion> suggestions = service.AnalyzeAllTables(nodes);

        Assert.NotEmpty(suggestions);
        var keys = suggestions
            .Select(s => $"{s.LeftColumn}|{s.RightColumn}|{s.JoinType}")
            .ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void AnalyzePair_ReturnsEmpty_WhenOneTableIdentifierIsMissing()
    {
        var service = new CanvasAutoJoinSuggestionService();
        NodeViewModel left = CreateTable("", ("id", PinDataType.Number));
        NodeViewModel right = CreateTable("public.customers", ("id", PinDataType.Number));
        var nodes = new List<NodeViewModel> { left, right };

        IReadOnlyList<JoinSuggestion> suggestions = service.AnalyzePair(left, right, nodes);

        Assert.Empty(suggestions);
    }

    private static NodeViewModel CreateTable(string fullName, params (string name, PinDataType type)[] columns)
        => new(fullName, columns, new Point(0, 0));
}


