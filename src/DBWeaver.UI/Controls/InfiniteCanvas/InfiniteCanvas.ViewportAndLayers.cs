using Avalonia;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Controls;

/// <summary>
/// Viewport and layer-order responsibilities extracted from InfiniteCanvas.
/// Keeps input/render pipeline lean while reusing shared math helpers.
/// </summary>
public sealed partial class InfiniteCanvas
{
    private Point ScreenToCanvas(Point s) =>
        new((s.X - _panOffset.X) / _zoom, (s.Y - _panOffset.Y) / _zoom);

    private bool CenterSelectionInView()
    {
        if (ViewModel is null)
            return false;

        if (!CanvasSelectionViewportMath.TryGetSelectionBounds(
            ViewModel.Nodes.Where(n => n.IsSelected),
            DefaultNodeH,
            out CanvasSelectionViewportMath.SelectionBounds bounds
        ))
            return false;

        _panOffset = CanvasSelectionViewportMath.ComputeCenterPan(bounds, Bounds.Size, _zoom);

        _isApplyingViewportFromCanvas = true;
        try
        {
            ViewModel.PanOffset = _panOffset;
        }
        finally
        {
            _isApplyingViewportFromCanvas = false;
        }

        SyncTransform();
        SyncWires();
        return true;
    }

    private bool FitSelectionInView()
    {
        if (ViewModel is null)
            return false;

        if (!CanvasSelectionViewportMath.TryGetSelectionBounds(
            ViewModel.Nodes.Where(n => n.IsSelected),
            DefaultNodeH,
            out CanvasSelectionViewportMath.SelectionBounds bounds
        ))
            return false;

        (double newZoom, Point newPan) = CanvasSelectionViewportMath.ComputeFit(
            bounds,
            Bounds.Size,
            padding: 40,
            minZoom: 0.15,
            maxZoom: 4.0
        );

        _zoom = newZoom;
        _panOffset = newPan;

        _isApplyingViewportFromCanvas = true;
        try
        {
            ViewModel.Zoom = _zoom;
            ViewModel.PanOffset = _panOffset;
        }
        finally
        {
            _isApplyingViewportFromCanvas = false;
        }

        SyncTransform();
        SyncWires();
        return true;
    }

    private bool BringSelectionToFront()
    {
        if (ViewModel is null)
            return false;
        bool changed = ViewModel.BringSelectionToFront();
        if (changed)
        {
            SyncWires();
            InvalidateArrange();
        }
        return changed;
    }

    private bool SendSelectionToBack()
    {
        if (ViewModel is null)
            return false;
        bool changed = ViewModel.SendSelectionToBack();
        if (changed)
        {
            SyncWires();
            InvalidateArrange();
        }
        return changed;
    }

    private bool BringSelectionForward()
    {
        if (ViewModel is null)
            return false;
        bool changed = ViewModel.BringSelectionForward();
        if (changed)
        {
            SyncWires();
            InvalidateArrange();
        }
        return changed;
    }

    private bool SendSelectionBackward()
    {
        if (ViewModel is null)
            return false;
        bool changed = ViewModel.SendSelectionBackward();
        if (changed)
        {
            SyncWires();
            InvalidateArrange();
        }
        return changed;
    }

    private bool NormalizeLayers()
    {
        if (ViewModel is null)
            return false;
        bool changed = ViewModel.NormalizeLayers();
        if (!changed)
            return false;
        NormalizeNodeZOrder();
        SyncWires();
        InvalidateArrange();
        return true;
    }

    private void NormalizeNodeZOrder()
    {
        if (ViewModel is null)
            return;

        int z = 0;
        foreach (NodeViewModel n in ViewModel.Nodes.OrderBy(n => n.ZOrder))
            n.ZOrder = z++;
    }
}
