using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainPlanComparisonBuilderTests
{
    [Fact]
    public void Build_AggregatesByOperation_AndComputesDelta()
    {
        var sut = new ExplainPlanComparisonBuilder();
        var a = new ExplainSnapshot(
            "A",
            DateTimeOffset.UtcNow,
            [
                new ExplainStep { Operation = "Seq Scan", EstimatedCost = 100 },
                new ExplainStep { Operation = "Sort", EstimatedCost = 50 },
            ]
        );
        var b = new ExplainSnapshot(
            "B",
            DateTimeOffset.UtcNow,
            [
                new ExplainStep { Operation = "Seq Scan", EstimatedCost = 20 },
                new ExplainStep { Operation = "Sort", EstimatedCost = 75 },
            ]
        );

        IReadOnlyList<ExplainComparisonRow> rows = sut.Build(a, b);

        ExplainComparisonRow seq = Assert.Single(rows, r => r.Operation == "Seq Scan");
        Assert.Equal(100, seq.CostA);
        Assert.Equal(20, seq.CostB);
        Assert.Equal("-80%", seq.DeltaText);
        Assert.Equal("IMPROVED", seq.StatusLabel);

        ExplainComparisonRow sort = Assert.Single(rows, r => r.Operation == "Sort");
        Assert.Equal("+50%", sort.DeltaText);
        Assert.Equal("REGRESSED", sort.StatusLabel);
    }

    [Fact]
    public void Build_IncludesOperationsPresentInOnlyOneSnapshot()
    {
        var sut = new ExplainPlanComparisonBuilder();
        var a = new ExplainSnapshot("A", DateTimeOffset.UtcNow, [new ExplainStep { Operation = "Hash", EstimatedCost = 10 }]);
        var b = new ExplainSnapshot("B", DateTimeOffset.UtcNow, [new ExplainStep { Operation = "Nested Loop", EstimatedCost = 12 }]);

        IReadOnlyList<ExplainComparisonRow> rows = sut.Build(a, b);

        ExplainComparisonRow hash = Assert.Single(rows, r => r.Operation == "Hash");
        Assert.Equal(10, hash.CostA);
        Assert.Equal(0, hash.CostB);

        ExplainComparisonRow loop = Assert.Single(rows, r => r.Operation == "Nested Loop");
        Assert.Equal(0, loop.CostA);
        Assert.Equal(12, loop.CostB);
        Assert.Equal("n/a", loop.DeltaText);
    }
}


