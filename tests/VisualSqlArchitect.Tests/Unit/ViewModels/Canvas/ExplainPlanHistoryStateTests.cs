using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainPlanHistoryStateTests
{
    [Fact]
    public void ImportHistoryState_ClampsToLatest10_AndExports()
    {
        var canvas = new CanvasViewModel();
        var sut = new ExplainPlanViewModel(canvas);
        var states = Enumerable.Range(1, 12)
            .Select(i => new ExplainHistoryState(
                TimestampUtc: new DateTimeOffset(2026, 4, 3, 0, i, 0, TimeSpan.Zero),
                TopOperation: $"Op {i}",
                TopCost: i,
                AlertCount: i % 2))
            .ToList();

        sut.ImportHistoryState(states);
        IReadOnlyList<ExplainHistoryState> exported = sut.ExportHistoryState();

        Assert.Equal(10, sut.History.Count);
        Assert.True(sut.HasHistory);
        Assert.Equal("Op 3", sut.History[0].TopOperation);
        Assert.Equal("Op 12", sut.History[^1].TopOperation);
        Assert.Equal(10, exported.Count);
    }

    [Fact]
    public void Close_PreservesHistorySnapshotForInspectorContinuity()
    {
        var canvas = new CanvasViewModel();
        var sut = new ExplainPlanViewModel(canvas);
        sut.ImportHistoryState(
        [
            new ExplainHistoryState(
                TimestampUtc: new DateTimeOffset(2026, 4, 9, 0, 0, 0, TimeSpan.Zero),
                TopOperation: "Seq Scan",
                TopCost: 120,
                AlertCount: 1),
        ]);

        sut.Close();

        Assert.Single(sut.History);
        Assert.True(sut.HasHistory);
    }
}

