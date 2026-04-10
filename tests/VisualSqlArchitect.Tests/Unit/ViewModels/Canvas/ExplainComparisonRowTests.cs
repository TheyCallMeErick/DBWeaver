using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainComparisonRowTests
{
    [Theory]
    [InlineData(100, 80, "-20%", true, false, "IMPROVED", "#86EFAC")]
    [InlineData(100, 120, "+20%", false, true, "REGRESSED", "#FCA5A5")]
    [InlineData(100, 100, "0%", false, false, "UNCHANGED", "#9CA3AF")]
    public void ComputedFields_ReflectDeltaState(
        double costA,
        double costB,
        string expectedDelta,
        bool isImproved,
        bool isRegressed,
        string expectedStatus,
        string expectedColor)
    {
        var row = new ExplainComparisonRow
        {
            Operation = "Seq Scan",
            CostA = costA,
            CostB = costB,
        };

        Assert.Equal(expectedDelta, row.DeltaText);
        Assert.Equal(isImproved, row.IsImproved);
        Assert.Equal(isRegressed, row.IsRegressed);
        Assert.Equal(expectedStatus, row.StatusLabel);
        Assert.Equal(expectedColor, row.DeltaColor);
        Assert.Equal(costA.ToString("0.##"), row.CostAText);
        Assert.Equal(costB.ToString("0.##"), row.CostBText);
    }

    [Fact]
    public void DeltaText_IsNa_WhenCostAIsZero()
    {
        var row = new ExplainComparisonRow
        {
            Operation = "Seq Scan",
            CostA = 0,
            CostB = 12,
        };

        Assert.Null(row.DeltaPercent);
        Assert.Equal("n/a", row.DeltaText);
        Assert.False(row.IsImproved);
        Assert.False(row.IsRegressed);
        Assert.Equal("UNCHANGED", row.StatusLabel);
    }
}


