using VisualSqlArchitect.UI.Controls;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Controls;

public class NodeControlBehaviorTests
{
    [Fact]
    public void NodeControl_DeclaresInteractiveTargetGuardMethod()
    {
        var method = typeof(NodeControl).GetMethod(
            "IsInteractiveTarget",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
        );
        Assert.NotNull(method);
        Assert.True(method!.IsPrivate);
        Assert.True(method.IsStatic);
    }

    [Fact]
    public void NodeControl_ExposesExpectedInteractionEvents()
    {
        var t = typeof(NodeControl);

        Assert.NotNull(t.GetEvent("NodeClicked"));
        Assert.NotNull(t.GetEvent("NodeDoubleClicked"));
        Assert.NotNull(t.GetEvent("NodeDragStarted"));
        Assert.NotNull(t.GetEvent("NodeDragDelta"));
        Assert.NotNull(t.GetEvent("NodeDragCompleted"));
        Assert.NotNull(t.GetEvent("PinPressed"));
    }
}
