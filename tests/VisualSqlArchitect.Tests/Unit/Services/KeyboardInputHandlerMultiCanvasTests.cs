using Avalonia.Input;
using DBWeaver.UI.Services;
using DBWeaver.UI.ViewModels;
using DBWeaver.Nodes;
using Xunit;

namespace DBWeaver.Tests.Unit.Services;

/// <summary>
/// Verifies that <see cref="KeyboardInputHandler"/> routes shortcuts to whichever
/// canvas is currently returned by the <see cref="IActiveCanvasProvider"/>,
/// not to any canvas captured at construction time.
/// </summary>
public class KeyboardInputHandlerMultiCanvasTests
{
    private static KeyboardInputHandler Build(IActiveCanvasProvider provider)
        => new(provider);

    // ── zoom routing ─────────────────────────────────────────────────────────

    [Fact]
    public void ZoomIn_RoutesToActiveCanvas_NotConstructionTimeCanvas()
    {
        var query = new CanvasViewModel();
        var ddl   = new CanvasViewModel();
        CanvasViewModel current = query;

        var handler = Build(new ActiveCanvasProvider(() => current));

        query.Zoom = 1.0;
        ddl.Zoom   = 1.0;

        handler.HandleShortcut(Key.OemPlus, KeyModifiers.Control);
        Assert.True(query.Zoom > 1.0);
        Assert.Equal(1.0, ddl.Zoom);
    }

    [Fact]
    public void ZoomIn_SwitchesToDdlCanvas_WhenProviderChanges()
    {
        var query = new CanvasViewModel();
        var ddl   = new CanvasViewModel();
        CanvasViewModel current = query;

        var handler = Build(new ActiveCanvasProvider(() => current));

        query.Zoom = 1.0;
        ddl.Zoom   = 1.0;

        // First press on query canvas.
        handler.HandleShortcut(Key.OemPlus, KeyModifiers.Control);
        double queryZoomAfterFirst = query.Zoom;

        // Switch active canvas.
        current = ddl;

        // Second press should affect DDL only.
        handler.HandleShortcut(Key.OemPlus, KeyModifiers.Control);
        Assert.Equal(queryZoomAfterFirst, query.Zoom); // query must not change again
        Assert.True(ddl.Zoom > 1.0);
    }

    // ── delete routing ───────────────────────────────────────────────────────

    [Fact]
    public void Delete_RoutesToActiveCanvas()
    {
        var query = new CanvasViewModel();
        var ddl   = new CanvasViewModel();

        query.Nodes.Add(new NodeViewModel("q.a", [], new Avalonia.Point(0, 0)) { IsSelected = true });
        ddl.Nodes.Add(new NodeViewModel("d.b",   [], new Avalonia.Point(0, 0)) { IsSelected = true });

        CanvasViewModel current = query;
        var handler = Build(new ActiveCanvasProvider(() => current));

        handler.HandleShortcut(Key.Delete, KeyModifiers.None);
        Assert.Empty(query.Nodes);
        Assert.Single(ddl.Nodes);

        current = ddl;
        handler.HandleShortcut(Key.Delete, KeyModifiers.None);
        Assert.Empty(ddl.Nodes);
    }

    // ── escape routing ───────────────────────────────────────────────────────

    [Fact]
    public void Escape_ClosesOverlayOnActiveCanvas_NotOnInactiveCanvas()
    {
        var query = new CanvasViewModel();
        var ddl   = new CanvasViewModel();

        ddl.DataPreview.IsVisible = true;

        CanvasViewModel current = ddl;
        var handler = Build(new ActiveCanvasProvider(() => current));

        bool handled = handler.HandleShortcut(Key.Escape, KeyModifiers.None);

        Assert.True(handled);
        Assert.False(ddl.DataPreview.IsVisible);
        Assert.False(query.DataPreview.IsVisible); // untouched
    }

    // ── undo routing ─────────────────────────────────────────────────────────

    [Fact]
    public void CtrlZ_UndoRoutesToActiveCanvas()
    {
        var query = new CanvasViewModel();
        query.Nodes.Clear();
        query.Connections.Clear();
        query.UndoRedo.Clear();

        query.Nodes.Add(new NodeViewModel("q.a", [], new Avalonia.Point(0, 0)) { IsSelected = true });

        CanvasViewModel current = query;
        var handler = Build(new ActiveCanvasProvider(() => current));

        // Delete to create an undo step.
        handler.HandleShortcut(Key.Delete, KeyModifiers.None);
        Assert.Empty(query.Nodes);

        // Undo while query is active.
        handler.HandleShortcut(Key.Z, KeyModifiers.Control);
        Assert.Single(query.Nodes);
    }

    // ── snap routing ─────────────────────────────────────────────────────────

    [Fact]
    public void CtrlG_TogglesSnapOnActiveCanvas_NotOnOther()
    {
        var query = new CanvasViewModel();
        var ddl   = new CanvasViewModel();

        bool queryInitial = query.SnapToGrid;
        bool ddlInitial   = ddl.SnapToGrid;

        CanvasViewModel current = query;
        var handler = Build(new ActiveCanvasProvider(() => current));

        handler.HandleShortcut(Key.G, KeyModifiers.Control);

        Assert.NotEqual(queryInitial, query.SnapToGrid);
        Assert.Equal(ddlInitial, ddl.SnapToGrid);
    }

    // ── provider is called each time ─────────────────────────────────────────

    [Fact]
    public void Provider_IsQueriedOnEachHandleShortcutCall()
    {
        var canvas = new CanvasViewModel();
        int callCount = 0;

        var provider = new ActiveCanvasProvider(() =>
        {
            callCount++;
            return canvas;
        });

        var handler = Build(provider);

        handler.HandleShortcut(Key.G, KeyModifiers.Control);
        handler.HandleShortcut(Key.G, KeyModifiers.Control);
        handler.HandleShortcut(Key.G, KeyModifiers.Control);

        // At minimum one resolution per HandleShortcut call.
        Assert.True(callCount >= 3);
    }
}
