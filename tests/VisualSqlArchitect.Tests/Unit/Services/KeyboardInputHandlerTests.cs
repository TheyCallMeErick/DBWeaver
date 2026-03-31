using VisualSqlArchitect.UI.Services;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Services;

/// <summary>
/// Tests for KeyboardInputHandler to ensure keyboard shortcuts are properly integrated.
/// Regression tests for bug where Ctrl+S and Ctrl+O were handled but not executed.
/// </summary>
public class KeyboardInputHandlerTests
{
    [Fact]
    public void Ctrl0_ResetsViewport()
    {
        var canvas = new CanvasViewModel();
        canvas.Zoom = 1.75;
        canvas.PanOffset = new Avalonia.Point(180, -90);

        var handler = new KeyboardInputHandler(canvas);

        bool handled = handler.HandleShortcut(Avalonia.Input.Key.D0, Avalonia.Input.KeyModifiers.Control);

        Assert.True(handled);
        Assert.Equal(1.0, canvas.Zoom);
        Assert.Equal(new Avalonia.Point(0, 0), canvas.PanOffset);
    }

    [Fact]
    public void F1_InvokesShortcutsCallback()
    {
        var canvas = new CanvasViewModel();
        bool opened = false;
        var handler = new KeyboardInputHandler(
            canvas,
            showShortcutsAction: () => opened = true
        );

        bool handled = handler.HandleShortcut(Avalonia.Input.Key.F1, Avalonia.Input.KeyModifiers.None);

        Assert.True(handled);
        Assert.True(opened);
    }

    [Fact]
    public void CtrlPageUp_BringsSelectionForward()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();
        canvas.UndoRedo.Clear();

        var a = new NodeViewModel("public.a", [], new Avalonia.Point(0, 0)) { ZOrder = 0 };
        var b = new NodeViewModel("public.b", [], new Avalonia.Point(100, 0)) { ZOrder = 1, IsSelected = true };
        var c = new NodeViewModel("public.c", [], new Avalonia.Point(200, 0)) { ZOrder = 2 };
        canvas.Nodes.Add(a);
        canvas.Nodes.Add(b);
        canvas.Nodes.Add(c);

        var handler = new KeyboardInputHandler(canvas);
        bool handled = handler.HandleShortcut(Avalonia.Input.Key.PageUp, Avalonia.Input.KeyModifiers.Control);

        Assert.True(handled);
        Assert.Equal(2, b.ZOrder);
    }

    [Fact]
    public void CtrlShiftPageDown_SendsSelectionToBack()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();
        canvas.UndoRedo.Clear();

        var a = new NodeViewModel("public.a", [], new Avalonia.Point(0, 0)) { ZOrder = 0 };
        var b = new NodeViewModel("public.b", [], new Avalonia.Point(100, 0)) { ZOrder = 1, IsSelected = true };
        var c = new NodeViewModel("public.c", [], new Avalonia.Point(200, 0)) { ZOrder = 2 };
        canvas.Nodes.Add(a);
        canvas.Nodes.Add(b);
        canvas.Nodes.Add(c);

        var handler = new KeyboardInputHandler(canvas);
        bool handled = handler.HandleShortcut(
            Avalonia.Input.Key.PageDown,
            Avalonia.Input.KeyModifiers.Control | Avalonia.Input.KeyModifiers.Shift
        );

        Assert.True(handled);
        Assert.Equal(0, b.ZOrder);
    }

    [Fact]
    public void CtrlF_OpensSearchCallback()
    {
        var canvas = new CanvasViewModel();
        bool opened = false;
        var handler = new KeyboardInputHandler(
            canvas,
            openSearchAction: () => opened = true
        );

        bool handled = handler.HandleShortcut(Avalonia.Input.Key.F, Avalonia.Input.KeyModifiers.Control);

        Assert.True(handled);
        Assert.True(opened);
    }

    [Fact]
    public void CanvasLocalKeys_AreNotHandledByGlobalHandler()
    {
        var canvas = new CanvasViewModel();
        var handler = new KeyboardInputHandler(canvas);

        Assert.False(handler.HandleShortcut(Avalonia.Input.Key.F, Avalonia.Input.KeyModifiers.None));
        Assert.False(handler.HandleShortcut(Avalonia.Input.Key.Left, Avalonia.Input.KeyModifiers.None));
        Assert.False(handler.HandleShortcut(Avalonia.Input.Key.Space, Avalonia.Input.KeyModifiers.None));
    }

    [Fact]
    public void Escape_ClosesOverlay_WhenVisible()
    {
        var canvas = new CanvasViewModel();
        canvas.CommandPalette.Open();
        var handler = new KeyboardInputHandler(canvas);

        bool handled = handler.HandleShortcut(Avalonia.Input.Key.Escape, Avalonia.Input.KeyModifiers.None);

        Assert.True(handled);
        Assert.False(canvas.CommandPalette.IsVisible);
    }

    [Fact]
    public void CtrlAltH_OpensFileHistoryOverlay()
    {
        var canvas = new CanvasViewModel();
        var handler = new KeyboardInputHandler(canvas);

        bool handled = handler.HandleShortcut(
            Avalonia.Input.Key.H,
            Avalonia.Input.KeyModifiers.Control | Avalonia.Input.KeyModifiers.Alt
        );

        Assert.True(handled);
        Assert.True(canvas.FileHistory.IsVisible);
    }
}
