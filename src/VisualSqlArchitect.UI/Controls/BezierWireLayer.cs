using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

/// <summary>
/// Transparent overlay canvas that draws all Bézier connection wires.
/// Sits behind all node controls but above the grid background.
/// Updates whenever any ConnectionViewModel changes (positions, hover state).
/// </summary>
public sealed class BezierWireLayer : Control
{
    // ── Avalonia Properties ───────────────────────────────────────────────────

    public static readonly StyledProperty<IReadOnlyList<ConnectionViewModel>> ConnectionsProperty =
        AvaloniaProperty.Register<BezierWireLayer, IReadOnlyList<ConnectionViewModel>>(
            nameof(Connections),
            defaultValue: []
        );

    public static readonly StyledProperty<ConnectionViewModel?> PendingConnectionProperty =
        AvaloniaProperty.Register<BezierWireLayer, ConnectionViewModel?>(nameof(PendingConnection));

    public IReadOnlyList<ConnectionViewModel> Connections
    {
        get => GetValue(ConnectionsProperty);
        set => SetValue(ConnectionsProperty, value);
    }

    public ConnectionViewModel? PendingConnection
    {
        get => GetValue(PendingConnectionProperty);
        set => SetValue(PendingConnectionProperty, value);
    }

    static BezierWireLayer()
    {
        ConnectionsProperty.Changed.AddClassHandler<BezierWireLayer>(
            (c, _) => c.InvalidateVisual()
        );
        PendingConnectionProperty.Changed.AddClassHandler<BezierWireLayer>(
            (c, _) => c.InvalidateVisual()
        );
        AffectsRender<BezierWireLayer>(ConnectionsProperty, PendingConnectionProperty);
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    public override void Render(DrawingContext dc)
    {
                foreach (ConnectionViewModel conn in Connections)
        {
                        DrawWire(dc, conn);
        }

        if (PendingConnection is not null)
            DrawWireDragging(dc, PendingConnection);
    }

    private static void DrawWire(DrawingContext dc, ConnectionViewModel conn)
    {
        Color color = conn.WireColor;
        double thickness = conn.WireThickness;
        Point from = conn.FromPoint;
        Point to = conn.ToPoint;

        (Point c1, Point c2) = BezierControlPoints(from, to);
        PathGeometry geometry = BuildBezier(from, c1, c2, to);

        // Glow (thick, low-alpha)
        var glowColor = Color.FromArgb(25, color.R, color.G, color.B);
        using (dc.PushOpacity(conn.IsHighlighted ? 0.7 : 0.4))
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(glowColor), thickness + 6), geometry);

        // Main wire
        var mainColor = Color.FromArgb(
            (byte)(conn.IsHighlighted ? 255 : 190),
            color.R,
            color.G,
            color.B
        );
        dc.DrawGeometry(
            null,
            new Pen(new SolidColorBrush(mainColor), thickness) { LineCap = PenLineCap.Round },
            geometry
        );

        // Endpoint dots
        DrawEndpointDot(dc, from, color, radius: 3.5);
        DrawEndpointDot(dc, to, color, radius: 3.5);
    }

    private static void DrawWireDragging(DrawingContext dc, ConnectionViewModel conn)
    {
        Color color = conn.WireColor;
        Point from = conn.FromPoint;
        Point to = conn.ToPoint;
        (Point c1, Point c2) = BezierControlPoints(from, to);
        PathGeometry geometry = BuildBezier(from, c1, c2, to);

        // Dashed animated-looking wire for pending connections
        using (dc.PushOpacity(0.7))
            dc.DrawGeometry(
                null,
                new Pen(new SolidColorBrush(color), 2)
                {
                    DashStyle = new DashStyle([6, 4], 0),
                    LineCap = PenLineCap.Round,
                },
                geometry
            );

        DrawEndpointDot(dc, from, color, radius: 4);
    }

    // ── Geometry helpers ──────────────────────────────────────────────────────

    private static (Point c1, Point c2) BezierControlPoints(Point from, Point to)
    {
        double dx = Math.Abs(to.X - from.X);
        double offset = Math.Max(60, dx * 0.5);
        return (new Point(from.X + offset, from.Y), new Point(to.X - offset, to.Y));
    }

    private static PathGeometry BuildBezier(Point from, Point c1, Point c2, Point to)
    {
        var seg = new BezierSegment
        {
            Point1 = c1,
            Point2 = c2,
            Point3 = to,
        };

        var figure = new PathFigure
        {
            StartPoint = from,
            IsClosed = false,
            IsFilled = false,
            Segments = [seg],
        };

        return new PathGeometry { Figures = [figure] };
    }

    private static void DrawEndpointDot(DrawingContext dc, Point center, Color color, double radius)
    {
        var brush = new SolidColorBrush(color);
        var bgBrush = new SolidColorBrush(Color.Parse("#171B26"));
        dc.DrawEllipse(bgBrush, null, center, radius + 1.5, radius + 1.5);
        dc.DrawEllipse(brush, null, center, radius, radius);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// DOT-GRID BACKGROUND
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Renders the infinite dot-grid background that pans and zooms with the canvas.
/// </summary>
public sealed class DotGridBackground : Control
{
    public static readonly StyledProperty<double> ZoomProperty = AvaloniaProperty.Register<
        DotGridBackground,
        double
    >(nameof(Zoom), 1.0);

    public static readonly StyledProperty<Point> PanOffsetProperty = AvaloniaProperty.Register<
        DotGridBackground,
        Point
    >(nameof(PanOffset));

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public Point PanOffset
    {
        get => GetValue(PanOffsetProperty);
        set => SetValue(PanOffsetProperty, value);
    }

    static DotGridBackground()
    {
        AffectsRender<DotGridBackground>(ZoomProperty, PanOffsetProperty);
    }

    public override void Render(DrawingContext dc)
    {
        // Base background fill
        dc.FillRectangle(new SolidColorBrush(Color.Parse("#0D0F14")), new Rect(Bounds.Size));

        const double baseSpacing = 28;
        double spacing = baseSpacing * Zoom;
        if (spacing < 6)
            return; // skip dots when too small

        double dotRadius = Math.Max(0.8, 1.2 * Zoom);
        var dotBrush = new SolidColorBrush(Color.Parse("#1E2330"));

        // Offset so dots pan with the canvas
        double offsetX = PanOffset.X % spacing;
        double offsetY = PanOffset.Y % spacing;

        for (double x = offsetX; x < Bounds.Width + spacing; x += spacing)
        for (double y = offsetY; y < Bounds.Height + spacing; y += spacing)
            dc.FillRectangle(
                dotBrush,
                new Rect(x - dotRadius, y - dotRadius, dotRadius * 2, dotRadius * 2)
            );

        // Subtle major grid lines every 5 cells
        double majorSpacing = spacing * 5;
        var majorBrush = new SolidColorBrush(Color.Parse("#161A22"));
        var majorPen = new Pen(majorBrush, 0.5);

        double majorOffX = PanOffset.X % majorSpacing;
        double majorOffY = PanOffset.Y % majorSpacing;

        for (double x = majorOffX; x < Bounds.Width + majorSpacing; x += majorSpacing)
            dc.DrawLine(majorPen, new Point(x, 0), new Point(x, Bounds.Height));
        for (double y = majorOffY; y < Bounds.Height + majorSpacing; y += majorSpacing)
            dc.DrawLine(majorPen, new Point(0, y), new Point(Bounds.Width, y));
    }
}

