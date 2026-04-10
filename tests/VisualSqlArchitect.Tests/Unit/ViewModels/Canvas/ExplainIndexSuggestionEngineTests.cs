using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.Core;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainIndexSuggestionEngineTests
{
    private static readonly IExplainIndexSuggestionEngine Sut = new ExplainIndexSuggestionEngine();

    [Fact]
    public void Build_ReturnsSuggestion_ForSeqScanWithFilter()
    {
        var steps = new List<ExplainStep>
        {
            new()
            {
                Operation = "Seq Scan",
                AlertLabel = "SEQ SCAN",
                EstimatedCost = 98,
                Detail = "relation=orders | filter=(orders.status = 'delivered')",
            },
        };

        IReadOnlyList<ExplainIndexSuggestion> suggestions = Sut.Build(steps, DatabaseProvider.Postgres);

        ExplainIndexSuggestion suggestion = Assert.Single(suggestions);
        Assert.Equal("orders", suggestion.Table);
        Assert.Contains("status", suggestion.Columns, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("CREATE INDEX CONCURRENTLY", suggestion.Sql);
        Assert.Contains("ON orders", suggestion.Sql);
    }

    [Fact]
    public void Build_DeduplicatesSameTableAndColumns()
    {
        var steps = new List<ExplainStep>
        {
            new()
            {
                Operation = "Seq Scan",
                AlertLabel = "SEQ SCAN",
                Detail = "relation=orders | filter=(orders.status = 'new')",
            },
            new()
            {
                Operation = "Seq Scan",
                AlertLabel = "SEQ SCAN",
                Detail = "relation=orders | filter=(status = 'paid')",
            },
        };

        IReadOnlyList<ExplainIndexSuggestion> suggestions = Sut.Build(steps, DatabaseProvider.Postgres);

        Assert.Single(suggestions);
    }

    [Fact]
    public void Build_ReturnsEmpty_ForNonPostgresOrNoFilter()
    {
        var steps = new List<ExplainStep>
        {
            new()
            {
                Operation = "Seq Scan",
                AlertLabel = "SEQ SCAN",
                Detail = "relation=orders",
            },
        };

        Assert.Empty(Sut.Build(steps, DatabaseProvider.SQLite));
        Assert.Empty(Sut.Build(steps, DatabaseProvider.Postgres));
    }
}

