using Avalonia;
using DBWeaver.CanvasKit;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Controls;

/// <summary>
/// Reusable math helpers for viewport operations over a node selection.
/// Pure functions: no UI references, safe for unit tests.
/// </summary>
public static class CanvasSelectionViewportMath
{
    public readonly record struct SelectionBounds(double MinX, double MinY, double MaxX, double MaxY)
    {
        public double Width => Math.Max(1, MaxX - MinX);
        public double Height => Math.Max(1, MaxY - MinY);
        public Point Center => new((MinX + MaxX) / 2.0, (MinY + MaxY) / 2.0);
    }

    public static bool TryGetSelectionBounds(
        IEnumerable<NodeViewModel> selected,
        double defaultNodeHeight,
        out SelectionBounds bounds
    )
    {
        IEnumerable<CanvasViewportNodeFrame> frames = selected.Select(n =>
            new CanvasViewportNodeFrame(
                n.Position.X,
                n.Position.Y,
                n.Width > 0 ? n.Width : 230,
                defaultNodeHeight
            ));

        if (!CanvasViewportMath.TryGetSelectionBounds(frames, out CanvasSelectionBounds coreBounds))
        {
            bounds = default;
            return false;
        }

        bounds = new SelectionBounds(coreBounds.MinX, coreBounds.MinY, coreBounds.MaxX, coreBounds.MaxY);
        return true;
    }

    public static Point ComputeCenterPan(SelectionBounds bounds, Size viewport, double zoom)
    {
        var coreBounds = new CanvasSelectionBounds(bounds.MinX, bounds.MinY, bounds.MaxX, bounds.MaxY);
        var coreViewport = new CanvasViewportSize(viewport.Width, viewport.Height);
        CanvasViewportPoint pan = CanvasViewportMath.ComputeCenterPan(coreBounds, coreViewport, zoom);
        return new Point(pan.X, pan.Y);
    }

    public static (double Zoom, Point Pan) ComputeFit(SelectionBounds bounds, Size viewport, double padding, double minZoom = 0.15, double maxZoom = 4.0)
    {
        var coreBounds = new CanvasSelectionBounds(bounds.MinX, bounds.MinY, bounds.MaxX, bounds.MaxY);
        var coreViewport = new CanvasViewportSize(viewport.Width, viewport.Height);
        (double zoom, CanvasViewportPoint pan) = CanvasViewportMath.ComputeFit(coreBounds, coreViewport, padding, minZoom, maxZoom);
        return (zoom, new Point(pan.X, pan.Y));
    }
}
