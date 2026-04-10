using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class CanvasAutoJoinAnalysisSafetyTests
{
    [Fact]
    public void TriggerAutoJoinAnalysis_IgnoresEmptyNewTableName()
    {
        var canvas = new CanvasViewModel();

        var ex = Record.Exception(() => canvas.TriggerAutoJoinAnalysis(""));

        Assert.Null(ex);
    }

    [Fact]
    public void AnalyzeAllCanvasJoins_HandlesNodesWithEmptyIdentifiers()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        var invalid = new NodeViewModel("", [], new Point(0, 0));
        var valid = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(200, 0));

        canvas.Nodes.Add(invalid);
        canvas.Nodes.Add(valid);

        var ex = Record.Exception(canvas.AnalyzeAllCanvasJoins);

        Assert.Null(ex);
    }
}


