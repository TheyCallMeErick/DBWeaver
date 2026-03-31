using Avalonia;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

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
        bounds = default;
        List<NodeViewModel> nodes = selected.ToList();
        if (nodes.Count == 0)
            return false;

        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;

        foreach (NodeViewModel n in nodes)
        {
            double w = n.Width > 0 ? n.Width : 230;
            minX = Math.Min(minX, n.Position.X);
            minY = Math.Min(minY, n.Position.Y);
            maxX = Math.Max(maxX, n.Position.X + w);
            maxY = Math.Max(maxY, n.Position.Y + defaultNodeHeight);
        }

        bounds = new SelectionBounds(minX, minY, maxX, maxY);
        return true;
    }

    public static Point ComputeCenterPan(SelectionBounds bounds, Size viewport, double zoom)
    {
        Point center = bounds.Center;
        return new Point(
            viewport.Width / 2.0 - center.X * zoom,
            viewport.Height / 2.0 - center.Y * zoom
        );
    }

    public static (double Zoom, Point Pan) ComputeFit(SelectionBounds bounds, Size viewport, double padding, double minZoom = 0.15, double maxZoom = 4.0)
    {
        double contentW = Math.Max(1, bounds.Width + padding * 2);
        double contentH = Math.Max(1, bounds.Height + padding * 2);

        double zoomX = viewport.Width / contentW;
        double zoomY = viewport.Height / contentH;
        double zoom = Math.Clamp(Math.Min(zoomX, zoomY), minZoom, maxZoom);

        Point pan = ComputeCenterPan(bounds, viewport, zoom);
        return (zoom, pan);
    }
}
