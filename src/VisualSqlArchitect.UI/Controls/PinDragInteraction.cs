using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

/// <summary>
/// Manages the drag-to-connect gesture on the canvas.
///
/// Flow:
///   1. User presses an output pin dot → BeginDrag()
///   2. Mouse moves → UpdateDrag() — draws a live wire to cursor
///   3. Mouse released:
///      a. Over a compatible input pin → EndDrag(targetPin) → ConnectPins()
///      b. Anywhere else              → CancelDrag()
///
/// The live wire is stored as <see cref="CanvasViewModel.PendingWire"/> and
/// rendered by <see cref="BezierWireLayer"/> identically to real connections
/// but with a dashed stroke.
///
/// This class is owned by <see cref="InfiniteCanvas"/> which calls it from
/// its pointer event handlers.
/// </summary>
public sealed class PinDragInteraction(CanvasViewModel vm, Canvas scene)
{
    private readonly CanvasViewModel _vm = vm;
    private readonly Canvas _scene = scene;
    private PinDragState? _dragState;

    // ── Public state (read by InfiniteCanvas) ─────────────────────────────────
    public bool IsDragging => _dragState is not null;
    public ConnectionViewModel? LiveWire => _dragState?.LiveWire;

    // ── Begin ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when the user presses on a pin dot.
    /// <paramref name="canvasPoint"/> is in canvas (unzoomed) coordinates.
    /// </summary>
    public void BeginDrag(PinViewModel pin, Point canvasPoint)
    {
        if (IsDragging)
            CancelDrag();

        // If pressing an input pin that is already connected, we pick up the
        // existing wire from its source (wire re-routing gesture).
        PinViewModel source = pin;
        ConnectionViewModel? existingWire = null;

        if (pin.Direction == Nodes.PinDirection.Input)
        {
            existingWire = _vm.Connections.FirstOrDefault(c => c.ToPin == pin);
            if (existingWire is not null)
            {
                // We grab the source end of the existing wire
                source = existingWire.FromPin;
                // Remove the connection so the user can re-route it
                _vm.DeleteConnection(existingWire);
                pin.IsConnected = _vm.Connections.Any(c => c.ToPin == pin);
            }
        }

        var liveWire = new ConnectionViewModel(source, source.AbsolutePosition, canvasPoint);
        _dragState = new PinDragState(source, liveWire, _vm.Nodes.SelectMany(n => n.AllPins));

        _vm.Connections.Add(liveWire); // renders immediately as a pending wire
    }

    // ── Update ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the endpoint of the live wire to follow the cursor.
    /// Also highlights nearby compatible pins.
    /// </summary>
    public void UpdateDrag(Point canvasPoint)
    {
        if (_dragState is null)
            return;
        _dragState.UpdateWireEnd(canvasPoint);

        // Highlight nearest valid target
        PinViewModel? nearest = _dragState.HitTest(canvasPoint, tol: 18);
        foreach (PinViewModel p in _dragState.ValidTargets)
            p.IsDropTarget = (p == nearest);
    }

    // ── End ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called on pointer-up. Checks for a valid drop target and completes
    /// or cancels the connection.
    /// </summary>
    public void EndDrag(Point canvasPoint)
    {
        if (_dragState is null)
            return;

        // Connect BEFORE removing the live wire.
        // If we removed the live wire first, Connections.CollectionChanged would fire
        // and SyncColumnListPins would destroy the target pin before we get to use it.
        PinViewModel? target = _dragState.HitTest(canvasPoint, tol: 18);
        if (target is not null && target.CanAccept(_dragState.SourcePin))
            _vm.ConnectPins(_dragState.SourcePin, target);

        // Remove the live wire after the real connection is already in place.
        _vm.Connections.Remove(_dragState.LiveWire);

        Cleanup();
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    public void CancelDrag()
    {
        if (_dragState is null)
            return;
        _vm.Connections.Remove(_dragState.LiveWire);
        Cleanup();
    }

    private void Cleanup()
    {
        _dragState?.Cancel();
        _dragState = null;
    }

    // ── Pin hit-test helper ───────────────────────────────────────────────────

    /// <summary>
    /// Checks whether <paramref name="canvasPoint"/> falls on any pin dot.
    /// Called by InfiniteCanvas to decide whether a pointer-press should start
    /// a pin drag instead of a node drag.
    /// </summary>
    public PinViewModel? HitTestPin(Point canvasPoint, double tolerance = 10)
    {
        double tolSq = tolerance * tolerance;
        foreach (NodeViewModel node in _vm.Nodes)
        foreach (PinViewModel pin in node.AllPins)
        {
            double dx = pin.AbsolutePosition.X - canvasPoint.X;
            double dy = pin.AbsolutePosition.Y - canvasPoint.Y;
            if (dx * dx + dy * dy <= tolSq)
                return pin;
        }
        return null;
    }
}
