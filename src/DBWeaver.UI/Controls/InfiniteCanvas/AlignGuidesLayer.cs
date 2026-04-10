using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DBWeaver.UI.Controls;

/// <summary>
/// Transparent overlay rendered on top of the canvas that draws horizontal and
/// vertical alignment guides while the user is dragging a node.
///
/// Guides are expressed as canvas-space coordinates and are converted to screen
/// space via the current zoom and pan offset before rendering.
/// </summary>
public sealed class AlignGuidesLayer : Control
{
    private static readonly Pen GuidePen = new(
        new SolidColorBrush(Color.FromArgb(200, 59, 130, 246)),
        1,
        lineCap: PenLineCap.Flat
    );

    public double Zoom { get; set; } = 1.0;
    public Point PanOffset { get; set; }

    private List<double> _hGuides = []; // canvas-space Y values
    private List<double> _vGuides = []; // canvas-space X values

    public void SetGuides(List<double> hGuides, List<double> vGuides)
    {
        _hGuides = hGuides;
        _vGuides = vGuides;
        InvalidateVisual();
    }

    public void ClearGuides()
    {
        if (_hGuides.Count == 0 && _vGuides.Count == 0)
            return;
        _hGuides = [];
        _vGuides = [];
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        if (_hGuides.Count == 0 && _vGuides.Count == 0)
            return;

        double w = Bounds.Width;
        double h = Bounds.Height;

        // Horizontal guides (fixed Y, span full width)
        foreach (double cy in _hGuides)
        {
            double sy = cy * Zoom + PanOffset.Y;
            ctx.DrawLine(GuidePen, new Point(0, sy), new Point(w, sy));
        }

        // Vertical guides (fixed X, span full height)
        foreach (double cx in _vGuides)
        {
            double sx = cx * Zoom + PanOffset.X;
            ctx.DrawLine(GuidePen, new Point(sx, 0), new Point(sx, h));
        }
    }
}
