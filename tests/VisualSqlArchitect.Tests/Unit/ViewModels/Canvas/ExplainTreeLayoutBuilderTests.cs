using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainTreeLayoutBuilderTests
{
    [Fact]
    public void Build_CreatesNodesAndEdges_FromIndentHierarchy()
    {
        var sut = new ExplainTreeLayoutBuilder();
        var steps = new List<ExplainStep>
        {
            new() { StepNumber = 1, Operation = "Limit", IndentLevel = 0, EstimatedCost = 210 },
            new() { StepNumber = 2, Operation = "Sort", IndentLevel = 1, EstimatedCost = 208 },
            new() { StepNumber = 3, Operation = "Hash Join", IndentLevel = 2, EstimatedCost = 178, AlertLabel = "HASH" },
            new() { StepNumber = 4, Operation = "Seq Scan", IndentLevel = 3, EstimatedCost = 98, AlertLabel = "SEQ SCAN" },
            new() { StepNumber = 5, Operation = "Hash", IndentLevel = 3, EstimatedCost = 80 },
        };

        ExplainTreeLayoutResult layout = sut.Build(steps);

        Assert.Equal(5, layout.Nodes.Count);
        Assert.Equal(4, layout.Edges.Count);
        Assert.True(layout.CanvasWidth > 0);
        Assert.True(layout.CanvasHeight > 0);
        Assert.Equal("Hash Join", layout.Nodes[2].Operation);
        Assert.True(layout.Nodes[2].HasAlert);
    }

    [Fact]
    public void Build_ReturnsEmptyLayout_WhenNoSteps()
    {
        var sut = new ExplainTreeLayoutBuilder();

        ExplainTreeLayoutResult layout = sut.Build([]);

        Assert.Empty(layout.Nodes);
        Assert.Empty(layout.Edges);
        Assert.Equal(0, layout.CanvasWidth);
        Assert.Equal(0, layout.CanvasHeight);
    }
}


