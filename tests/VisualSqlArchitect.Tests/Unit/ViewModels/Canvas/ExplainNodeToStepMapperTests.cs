using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainNodeToStepMapperTests
{
    [Fact]
    public void Map_ConvertsNodesToSequentialSteps()
    {
        var nodes = new List<ExplainNode>
        {
            new()
            {
                NodeId = "n1",
                NodeType = "Sort",
                Detail = "ORDER BY",
                RelationName = "orders",
                IndexName = "ix_orders_created_at",
                StartupCost = 1.2,
                EstimatedCost = 10.5,
                EstimatedRows = 100,
                ActualStartupTimeMs = 0.1,
                ActualTotalTimeMs = 1.7,
                ActualLoops = 2,
                IndentLevel = 1,
                IsExpensive = true,
                AlertLabel = "SORT",
            },
            new()
            {
                NodeId = "n2",
                ParentNodeId = "n1",
                NodeType = "Seq Scan",
                Detail = "SCAN orders",
                EstimatedRows = 1000,
                ActualRows = 4200,
                IndentLevel = 2,
                IsExpensive = true,
                AlertLabel = "SEQ SCAN",
            },
        };

        var sut = new ExplainNodeToStepMapper();
        IReadOnlyList<ExplainStep> steps = sut.Map(nodes);

        Assert.Equal(2, steps.Count);
        Assert.Equal(1, steps[0].StepNumber);
        Assert.Equal(2, steps[1].StepNumber);
        Assert.Equal("Sort", steps[0].Operation);
        Assert.Equal("SCAN orders", steps[1].Detail);
        Assert.Equal("n2", steps[1].NodeId);
        Assert.Equal("n1", steps[1].ParentNodeId);
        Assert.Equal("orders", steps[0].RelationName);
        Assert.Equal("ix_orders_created_at", steps[0].IndexName);
        Assert.Equal(1.2, steps[0].StartupCost);
        Assert.Equal(1.7, steps[0].ActualTotalTimeMs);
        Assert.Equal(2, steps[0].ActualLoops);
        Assert.Equal(4200, steps[1].ActualRows);
        Assert.Equal("SEQ SCAN", steps[1].AlertLabel);
    }
}


