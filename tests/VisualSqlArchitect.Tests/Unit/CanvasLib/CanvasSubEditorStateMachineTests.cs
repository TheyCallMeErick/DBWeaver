using DBWeaver.CanvasKit;

namespace DBWeaver.Tests.Unit.CanvasLib;

public sealed class CanvasSubEditorStateMachineTests
{
    [Fact]
    public void EnterCte_CreatesActiveCteState()
    {
        CanvasSubEditorSessionState state = CanvasSubEditorStateMachine.EnterCte("orders_cte");

        Assert.True(state.IsActive);
        Assert.False(state.IsViewEditor);
        Assert.Equal(CanvasSubEditorMode.Cte, state.Mode);
        Assert.Equal("orders_cte", state.DisplayName);
    }

    [Fact]
    public void EnterView_TrimDisplayName()
    {
        CanvasSubEditorSessionState state = CanvasSubEditorStateMachine.EnterView("  v_orders  ");

        Assert.True(state.IsActive);
        Assert.True(state.IsViewEditor);
        Assert.Equal(CanvasSubEditorMode.View, state.Mode);
        Assert.Equal("v_orders", state.DisplayName);
    }

    [Fact]
    public void Exit_ReturnsEmptyState()
    {
        CanvasSubEditorSessionState state = CanvasSubEditorStateMachine.Exit();

        Assert.False(state.IsActive);
        Assert.False(state.IsViewEditor);
        Assert.Equal(CanvasSubEditorMode.None, state.Mode);
        Assert.Equal(string.Empty, state.DisplayName);
    }
}
