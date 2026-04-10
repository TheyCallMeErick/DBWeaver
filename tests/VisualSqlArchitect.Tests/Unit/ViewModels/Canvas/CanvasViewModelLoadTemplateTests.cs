using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class CanvasViewModelLoadTemplateTests
{
    [Fact]
    public void LoadTemplate_ResetsCanvasState_AndBuildsTemplateGraph()
    {
        var canvas = new CanvasViewModel();

        canvas.SpawnNode(NodeDefinitionRegistry.Get(NodeType.Equals), new Point(120, 80));
        canvas.CurrentFilePath = "query.vsa";
        canvas.QueryText = "SELECT 1";
        canvas.Zoom = 1.75;
        canvas.PanOffset = new Point(45, 90);
        canvas.IsDirty = true;

        QueryTemplate template = QueryTemplateLibrary.All.First(t => t.Name == "Simple SELECT");
        canvas.LoadTemplate(template);

        Assert.Null(canvas.CurrentFilePath);
        Assert.Equal(string.Empty, canvas.QueryText);
        Assert.Equal(1.0, canvas.Zoom);
        Assert.Equal(new Point(0, 0), canvas.PanOffset);
        Assert.False(canvas.IsDirty);

        Assert.NotEmpty(canvas.Nodes);
        Assert.Contains(canvas.Nodes, n => n.Type == NodeType.ResultOutput);
        Assert.NotEmpty(canvas.Connections);

        Assert.True(canvas.UndoRedo.CanUndo);
    }
}


