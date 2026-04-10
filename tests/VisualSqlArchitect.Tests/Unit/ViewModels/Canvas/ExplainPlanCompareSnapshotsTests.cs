using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainPlanCompareSnapshotsTests
{
    [Fact]
    public void CaptureSnapshot_AddsSnapshot_AndBuildsComparison_WhenTwoSnapshotsExist()
    {
        var canvas = new CanvasViewModel();
        var sut = new ExplainPlanViewModel(canvas);

        sut.Steps.Add(new ExplainStep { Operation = "Seq Scan", EstimatedCost = 100 });
        sut.CaptureSnapshot();
        Assert.Single(sut.Snapshots);
        Assert.False(sut.HasComparisonRows);

        sut.Steps.Clear();
        sut.Steps.Add(new ExplainStep { Operation = "Seq Scan", EstimatedCost = 50 });
        sut.CaptureSnapshot();

        Assert.Equal(2, sut.Snapshots.Count);
        Assert.NotNull(sut.SelectedSnapshotA);
        Assert.NotNull(sut.SelectedSnapshotB);
        Assert.True(sut.HasComparisonRows);
        ExplainComparisonRow row = Assert.Single(sut.ComparisonRows);
        Assert.Equal("Seq Scan", row.Operation);
        Assert.Equal("-50%", row.DeltaText);
    }

    [Fact]
    public void CaptureSnapshot_KeepsOnlyLatestFive()
    {
        var canvas = new CanvasViewModel();
        var sut = new ExplainPlanViewModel(canvas);

        for (int i = 0; i < 7; i++)
        {
            sut.Steps.Clear();
            sut.Steps.Add(new ExplainStep { Operation = $"Step {i}", EstimatedCost = i + 1 });
            sut.CaptureSnapshot();
        }

        Assert.Equal(5, sut.Snapshots.Count);
        Assert.Equal("Snapshot 3", sut.Snapshots[0].Label);
        Assert.Equal("Snapshot 7", sut.Snapshots[^1].Label);
    }
}


