using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainPlanTreeModeTests
{
    [Fact]
    public void SetTreeMode_TogglesViewFlags()
    {
        var canvas = new CanvasViewModel();
        var sut = new ExplainPlanViewModel(canvas);
        sut.Steps.Add(new ExplainStep { Operation = "Seq Scan", EstimatedCost = 10 });

        sut.SetTreeMode();

        Assert.True(sut.IsTreeMode);
        Assert.False(sut.IsListMode);
        Assert.True(sut.ShowTreeView);
        Assert.False(sut.ShowListView);

        sut.SetListMode();
        Assert.False(sut.IsTreeMode);
        Assert.True(sut.IsListMode);
        Assert.False(sut.ShowTreeView);
        Assert.True(sut.ShowListView);
    }

    [Fact]
    public void RefreshTreeLayout_BuildsVisualNodesFromSteps()
    {
        var canvas = new CanvasViewModel();
        var sut = new ExplainPlanViewModel(canvas);
        sut.Steps.Add(new ExplainStep { Operation = "Limit", IndentLevel = 0, EstimatedCost = 20 });
        sut.Steps.Add(new ExplainStep { Operation = "Seq Scan", IndentLevel = 1, EstimatedCost = 18, AlertLabel = "SEQ SCAN" });

        sut.RefreshTreeLayout();

        Assert.Equal(2, sut.TreeNodes.Count);
        Assert.Single(sut.TreeEdges);
        Assert.True(sut.TreeCanvasWidth > 0);
        Assert.True(sut.TreeCanvasHeight > 0);
    }
}


