namespace DBWeaver.CanvasKit;

public readonly record struct CanvasViewportPoint(double X, double Y);

public readonly record struct CanvasViewportSize(double Width, double Height);

public readonly record struct CanvasViewportNodeFrame(double X, double Y, double Width, double Height);

public readonly record struct CanvasSelectionBounds(double MinX, double MinY, double MaxX, double MaxY)
{
    public double Width => Math.Max(1, MaxX - MinX);
    public double Height => Math.Max(1, MaxY - MinY);
    public CanvasViewportPoint Center => new((MinX + MaxX) / 2.0, (MinY + MaxY) / 2.0);
}

public static class CanvasViewportMath
{
    public static bool TryGetSelectionBounds(
        IEnumerable<CanvasViewportNodeFrame> selected,
        out CanvasSelectionBounds bounds)
    {
        bounds = default;

        List<CanvasViewportNodeFrame> nodes = selected.ToList();
        if (nodes.Count == 0)
            return false;

        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;

        foreach (CanvasViewportNodeFrame n in nodes)
        {
            minX = Math.Min(minX, n.X);
            minY = Math.Min(minY, n.Y);
            maxX = Math.Max(maxX, n.X + Math.Max(1, n.Width));
            maxY = Math.Max(maxY, n.Y + Math.Max(1, n.Height));
        }

        bounds = new CanvasSelectionBounds(minX, minY, maxX, maxY);
        return true;
    }

    public static CanvasViewportPoint ComputeCenterPan(
        CanvasSelectionBounds bounds,
        CanvasViewportSize viewport,
        double zoom)
    {
        CanvasViewportPoint center = bounds.Center;
        return new CanvasViewportPoint(
            viewport.Width / 2.0 - center.X * zoom,
            viewport.Height / 2.0 - center.Y * zoom
        );
    }

    public static (double Zoom, CanvasViewportPoint Pan) ComputeFit(
        CanvasSelectionBounds bounds,
        CanvasViewportSize viewport,
        double padding,
        double minZoom = 0.15,
        double maxZoom = 4.0)
    {
        double contentW = Math.Max(1, bounds.Width + padding * 2);
        double contentH = Math.Max(1, bounds.Height + padding * 2);

        double zoomX = viewport.Width / contentW;
        double zoomY = viewport.Height / contentH;
        double zoom = Math.Clamp(Math.Min(zoomX, zoomY), minZoom, maxZoom);

        CanvasViewportPoint pan = ComputeCenterPan(bounds, viewport, zoom);
        return (zoom, pan);
    }
}
