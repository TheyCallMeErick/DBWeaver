using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainHistoryModelsTests
{
    [Fact]
    public void HistoryItem_ToStateAndFromState_RoundTrip()
    {
        var item = new ExplainHistoryItem
        {
            TimestampUtc = new DateTimeOffset(2026, 4, 3, 10, 20, 0, TimeSpan.Zero),
            TopOperation = "Seq Scan",
            TopCost = 98.4,
            AlertCount = 2,
        };

        ExplainHistoryState state = item.ToState();
        ExplainHistoryItem clone = ExplainHistoryItem.FromState(state);

        Assert.Equal(item.TimestampUtc, clone.TimestampUtc);
        Assert.Equal(item.TopOperation, clone.TopOperation);
        Assert.Equal(item.TopCost, clone.TopCost);
        Assert.Equal(item.AlertCount, clone.AlertCount);
        Assert.Equal("98.4", clone.CostText);
        Assert.Equal("2 alertas", clone.AlertText);
    }
}


