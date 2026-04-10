using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainCostDistributionCalculatorTests
{
    private static ExplainStep Step(double? cost, int indent = 0) =>
        new()
        {
            Operation = "op",
            EstimatedCost = cost,
            IndentLevel = indent,
        };

    [Fact]
    public void Apply_UsesRootNodeCost_AsBaseline()
    {
        var steps = new List<ExplainStep>
        {
            Step(200, indent: 0),
            Step(50, indent: 1),
            Step(null, indent: 1),
        };

        var sut = new ExplainCostDistributionCalculator();
        sut.Apply(steps);

        Assert.Equal(1d, steps[0].CostFraction);
        Assert.Equal(0.25d, steps[1].CostFraction);
        Assert.Null(steps[2].CostFraction);
        Assert.Equal("25%", steps[1].CostFractionText);
    }

    [Fact]
    public void Apply_FallsBackToHighestCost_WhenRootCostMissing()
    {
        var steps = new List<ExplainStep>
        {
            Step(10, indent: 1),
            Step(40, indent: 2),
            Step(20, indent: 1),
        };

        var sut = new ExplainCostDistributionCalculator();
        sut.Apply(steps);

        Assert.Equal(0.25d, steps[0].CostFraction);
        Assert.Equal(1d, steps[1].CostFraction);
        Assert.Equal(0.5d, steps[2].CostFraction);
    }

    [Fact]
    public void Apply_ClampsValuesAboveOneHundredPercent()
    {
        var steps = new List<ExplainStep>
        {
            Step(10, indent: 0),
            Step(25, indent: 1),
        };

        var sut = new ExplainCostDistributionCalculator();
        sut.Apply(steps);

        Assert.Equal(1d, steps[0].CostFraction);
        Assert.Equal(1d, steps[1].CostFraction);
        Assert.Equal("100%", steps[1].CostFractionText);
    }

    [Fact]
    public void Apply_LeavesFractionsNull_WhenNoPositiveCosts()
    {
        var steps = new List<ExplainStep>
        {
            Step(null),
            Step(0),
            Step(-10),
        };

        var sut = new ExplainCostDistributionCalculator();
        sut.Apply(steps);

        Assert.All(steps, s => Assert.Null(s.CostFraction));
        Assert.All(steps, s => Assert.Equal("–", s.CostFractionText));
    }

    [Fact]
    public void ExplainStep_FormatsCorePresentationProperties()
    {
        var step = new ExplainStep
        {
            Operation = "Seq Scan",
            EstimatedCost = 123.456,
            EstimatedRows = 12345,
            IndentLevel = 2,
            CostFraction = 0.376,
        };

        Assert.Equal(36d, step.IndentMargin);
        Assert.Equal("123.46", step.CostText);
        Assert.Equal("12,345", step.RowsText);
        Assert.Equal("37.6%", step.CostFractionText);
        Assert.True(step.HasCostBar);
        Assert.Equal(36.096, step.CostBarWidth, precision: 3);
        Assert.Equal("#F59E0B", step.CostBarFill);
    }

    [Theory]
    [InlineData(0.75, "#F97316")]
    [InlineData(0.40, "#F59E0B")]
    [InlineData(0.10, "#3B82F6")]
    [InlineData(null, "#1F2937")]
    public void ExplainStep_ResolvesCostBarColorByFraction(double? fraction, string expectedColor)
    {
        var step = new ExplainStep { CostFraction = fraction };
        Assert.Equal(expectedColor, step.CostBarFill);
    }

    [Theory]
    [InlineData("SEQ SCAN", "#FBBF24")]
    [InlineData("SORT", "#FB923C")]
    [InlineData("HASH", "#60A5FA")]
    [InlineData("LOOP", "#A78BFA")]
    [InlineData("", "#6B7280")]
    public void ExplainStep_ResolvesAlertColorByLabel(string label, string expectedColor)
    {
        var step = new ExplainStep { AlertLabel = label };
        Assert.Equal(expectedColor, step.AlertColor);
    }

    [Fact]
    public void ExplainStep_HasAlert_IsTrueOnlyWhenLabelExists()
    {
        var noAlert = new ExplainStep { AlertLabel = "" };
        var withAlert = new ExplainStep { AlertLabel = "SEQ SCAN" };

        Assert.False(noAlert.HasAlert);
        Assert.True(withAlert.HasAlert);
    }

    [Fact]
    public void ExplainStep_ComputesRowsErrorAndStaleStatsBadge()
    {
        var step = new ExplainStep
        {
            EstimatedRows = 500,
            ActualRows = 48231,
        };

        Assert.True(step.HasRowsError);
        Assert.True(step.RowsErrorRatio > 90);
        Assert.Equal("95.5x", step.RowsErrorText);
        Assert.True(step.IsStaleStats);
        Assert.True(step.HasStaleStatsBadge);
        Assert.Equal("#A78BFA", step.RowsErrorColor);
        Assert.Equal("STALE STATS", step.StaleStatsLabel);
    }

}

