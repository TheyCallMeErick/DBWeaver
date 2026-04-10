using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainPlanIndexSuggestionsTests
{
    [Fact]
    public void SelectIndexSuggestion_StoresSqlForUiConsumption()
    {
        var canvas = new CanvasViewModel();
        var sut = new ExplainPlanViewModel(canvas);
        var suggestion = new ExplainIndexSuggestion
        {
            Table = "orders",
            Columns = ["status"],
            Reason = "reason",
            Sql = "CREATE INDEX CONCURRENTLY idx_orders_status ON orders (status);",
        };

        sut.SelectIndexSuggestion(suggestion);

        Assert.Equal(suggestion.Sql, sut.SelectedSuggestionSql);
    }
}


