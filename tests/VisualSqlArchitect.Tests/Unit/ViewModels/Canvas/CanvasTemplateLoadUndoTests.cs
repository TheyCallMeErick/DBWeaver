using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class CanvasTemplateLoadUndoTests
{
    [Fact]
    public void LoadTemplate_IsUndoable_AndRestoresPreviousCanvasState()
    {
        var canvas = new CanvasViewModel();

        int beforeNodeCount = canvas.Nodes.Count;

        var template = new QueryTemplate(
            Name: "Test Template",
            Description: "Builds a minimal canvas",
            Category: "Tests",
            Tags: "undo",
            Build: vm =>
            {
                vm.Nodes.Add(new NodeViewModel("public.customers", [], new Point(100, 100)));
            });

        canvas.LoadTemplate(template);

        Assert.True(canvas.UndoRedo.CanUndo);
        Assert.Single(canvas.Nodes);

        canvas.UndoRedo.Undo();

        Assert.Equal(beforeNodeCount, canvas.Nodes.Count);
    }
}


