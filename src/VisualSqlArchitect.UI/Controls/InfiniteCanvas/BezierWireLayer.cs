using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using System.Linq;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

/// <summary>
/// Transparent overlay canvas that draws all Bézier connection wires.
/// Sits behind all node controls but above the grid background.
///
/// Performance optimizations applied (Tarefa 18):
///   • Geometry cache: <see cref="PathGeometry"/> is only rebuilt when endpoints change.
///   • Brush/pen cache: pens and brushes are reused across frames, keyed by color+thickness.
///   • Viewport culling: wires whose AABB lies entirely outside <see cref="Bounds"/> are skipped.
///   • Static shared brushes for constant-color elements (endpoint background, grid bg).
/// </summary>
public sealed class BezierWireLayer : Control
{
    private sealed record RemovalFlash(
        Point From,
        Point To,
        Color Color,
        double Thickness,
        ConnectionViewModel.WireDashKind DashKind,
        long StartedAtMs
    );

    private readonly List<RemovalFlash> _removalFlashes = [];
    private const long RemovalFlashDurationMs = 180;

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
        ConnectionsProperty.Changed.AddClassHandler<BezierWireLayer>((c, _) =>
        {
            c.PruneGeomCache();
            c.InvalidateVisual();
        });
        PendingConnectionProperty.Changed.AddClassHandler<BezierWireLayer>(
            (c, _) => c.InvalidateVisual()
        );
        AffectsRender<BezierWireLayer>(ConnectionsProperty, PendingConnectionProperty);
    }

    // ── Geometry cache ────────────────────────────────────────────────────────

    private readonly record struct WireEndpoints(Point From, Point To);

    /// <summary>Cached geometry per connection — rebuilt only when endpoints change.</summary>
    private readonly Dictionary<ConnectionViewModel, (PathGeometry Geom, WireEndpoints Ends)>
        _geomCache = new();

    /// <summary>
    /// Removes geometry cache entries for connections that are no longer in the list.
    /// Called whenever the Connections property changes to prevent unbounded cache growth.
    /// Avoids intermediate allocations when the cache is already fully valid.
    /// </summary>
    private void PruneGeomCache()
    {
        if (_geomCache.Count == 0)
            return;

        var current = new HashSet<ConnectionViewModel>(Connections);

        // Collect stale keys in a separate pass to avoid "modified during enumeration".
        // Only allocates when there are actually stale entries.
        List<ConnectionViewModel>? stale = null;
        foreach (ConnectionViewModel key in _geomCache.Keys)
        {
            if (!current.Contains(key))
                (stale ??= []).Add(key);
        }

        if (stale is not null)
            foreach (ConnectionViewModel key in stale)
                _geomCache.Remove(key);
    }

    // ── Brush / Pen cache ─────────────────────────────────────────────────────

    private readonly record struct PenKey(
        Color Color,
        double Thickness,
        bool IsGlow,
        ConnectionViewModel.WireDashKind DashKind
    );

    private readonly Dictionary<PenKey, Pen> _penCache = new();
    private readonly Dictionary<Color, SolidColorBrush> _brushCache = new();
    private readonly Dictionary<Color, Pen> _dragPenCache = new();
    private static readonly DashStyle DragDashStyle = new([6, 4], 0);
    private static readonly DashStyle ColumnSetDashStyle = new([8, 3], 0);
    private static readonly DashStyle RowSetDashStyle = new([12, 4], 0);
    private static readonly DashStyle ExpressionDashStyle = new([2, 4], 0);

    /// <summary>Static background brush for endpoint dots (#171B26) — shared across all wires.</summary>
    private static readonly SolidColorBrush DotBgBrush =
        new(Color.Parse("#171B26")) { Opacity = 1 };

    private Pen GetPen(
        Color color,
        double thickness,
        ConnectionViewModel.WireDashKind dashKind = ConnectionViewModel.WireDashKind.Solid,
        bool isGlow = false
    )
    {
        var cacheDashKind = isGlow ? ConnectionViewModel.WireDashKind.Solid : dashKind;
        var key = new PenKey(color, thickness, isGlow, cacheDashKind);
        if (!_penCache.TryGetValue(key, out Pen? pen))
        {
            SolidColorBrush brush = GetSolidBrush(color);
            pen = isGlow
                ? new Pen(brush, thickness)
                : new Pen(brush, thickness) { LineCap = PenLineCap.Round };

            if (!isGlow)
            {
                pen.DashStyle = dashKind switch
                {
                    ConnectionViewModel.WireDashKind.LongDash => ColumnSetDashStyle,
                    ConnectionViewModel.WireDashKind.WideDash => RowSetDashStyle,
                    ConnectionViewModel.WireDashKind.Dotted => ExpressionDashStyle,
                    _ => null,
                };
            }

            _penCache[key] = pen;
        }
        return pen;
    }

    private SolidColorBrush GetSolidBrush(Color color)
    {
        if (!_brushCache.TryGetValue(color, out SolidColorBrush? brush))
        {
            brush = new SolidColorBrush(color);
            _brushCache[color] = brush;
        }

        return brush;
    }

    private Pen GetDragPen(Color color)
    {
        if (!_dragPenCache.TryGetValue(color, out Pen? pen))
        {
            pen = new Pen(GetSolidBrush(color), 2)
            {
                DashStyle = DragDashStyle,
                LineCap = PenLineCap.Round,
            };
            _dragPenCache[color] = pen;
        }

        return pen;
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    public override void Render(DrawingContext dc)
    {
        // Bounds can be transiently zero right after topology/materialization updates.
        // In that case, disable aggressive culling by using a safe virtual viewport.
        Rect viewport = Bounds.Width > 1 && Bounds.Height > 1
            ? new Rect(Bounds.Size)
            : new Rect(0, 0, 20000, 20000);

        foreach (ConnectionViewModel conn in Connections
                     .Where(c => c.ToPin is not null)
                     .OrderBy(GetConnectionDepth)
                     .ThenBy(GetEmphasisLevel))
            DrawWire(dc, conn, viewport);

        DrawRemovalFlashes(dc, viewport);

        if (PendingConnection is not null && PendingConnection.ToPin is null)
            DrawWireDragging(dc, PendingConnection);
    }

    /// <summary>
    /// Adds a short-lived visual flash for a connection being removed,
    /// providing immediate deletion feedback to the user.
    /// </summary>
    public void AddRemovalFlash(ConnectionViewModel conn)
    {
        if (conn.ToPin is null)
            return;

        _removalFlashes.Add(
            new RemovalFlash(
                conn.FromPoint,
                conn.ToPoint,
                conn.WireColor,
                conn.WireThickness,
                conn.DashKind,
                Environment.TickCount64
            )
        );

        InvalidateVisual();
    }

    /// <summary>
    /// Returns the top-most wire near <paramref name="point"/>, or null when none is within tolerance.
    /// Mirrors the same depth/emphasis ordering used in render so hover feels consistent.
    /// </summary>
    public ConnectionViewModel? HitTestWire(Point point, double tolerance = 8)
    {
        ConnectionViewModel? best = null;
        int bestDepth = int.MinValue;
        int bestEmphasis = int.MinValue;

        foreach (ConnectionViewModel conn in Connections.Where(c => c.ToPin is not null))
        {
            if (!IsPointNearBezier(point, conn.FromPoint, conn.ToPoint, tolerance))
                continue;

            int depth = GetConnectionDepth(conn);
            int emphasis = GetEmphasisLevel(conn);
            if (best is null
                || depth > bestDepth
                || (depth == bestDepth && emphasis > bestEmphasis))
            {
                best = conn;
                bestDepth = depth;
                bestEmphasis = emphasis;
            }
        }

        return best;
    }

    private static int GetConnectionDepth(ConnectionViewModel conn)
    {
        int fromDepth = conn.FromPin.Owner.ZOrder;
        int toDepth = conn.ToPin?.Owner.ZOrder ?? fromDepth;
        return Math.Min(fromDepth, toDepth);
    }

    private static bool IsConnectionEmphasized(ConnectionViewModel conn) =>
        GetEmphasisLevel(conn) > 0;

    private static int GetEmphasisLevel(ConnectionViewModel conn)
    {
        if (conn.IsHighlighted)
            return 3;

        bool fromSelected = conn.FromPin.Owner.IsSelected;
        bool toSelected = conn.ToPin?.Owner.IsSelected ?? false;

        if (fromSelected && toSelected)
            return 2;
        if (fromSelected || toSelected)
            return 1;
        return 0;
    }

    private static Color BoostForEmphasis(Color baseColor, int emphasisLevel)
    {
        if (emphasisLevel <= 0)
            return baseColor;

        double t = emphasisLevel >= 3 ? 0.32 : emphasisLevel == 2 ? 0.22 : 0.12;
        byte r = (byte)Math.Clamp(baseColor.R + (255 - baseColor.R) * t, 0, 255);
        byte g = (byte)Math.Clamp(baseColor.G + (255 - baseColor.G) * t, 0, 255);
        byte b = (byte)Math.Clamp(baseColor.B + (255 - baseColor.B) * t, 0, 255);
        return Color.FromArgb(baseColor.A, r, g, b);
    }

    private void DrawWire(DrawingContext dc, ConnectionViewModel conn, Rect viewport)
    {
        Point from = conn.FromPoint;
        Point to   = conn.ToPoint;
        int emphasisLevel = GetEmphasisLevel(conn);
        bool emphasized = emphasisLevel > 0;

        // ── Viewport culling ──────────────────────────────────────────────────
        // Use the bounding box of control points to estimate wire extent.
        (Point c1, Point c2) = BezierControlPoints(from, to);
        double minX = Math.Min(Math.Min(from.X, to.X), Math.Min(c1.X, c2.X));
        double minY = Math.Min(Math.Min(from.Y, to.Y), Math.Min(c1.Y, c2.Y));
        double maxX = Math.Max(Math.Max(from.X, to.X), Math.Max(c1.X, c2.X));
        double maxY = Math.Max(Math.Max(from.Y, to.Y), Math.Max(c1.Y, c2.Y));
        if (maxX < 0 || minX > viewport.Width || maxY < 0 || minY > viewport.Height)
            return; // entirely outside viewport

        // ── Geometry cache ────────────────────────────────────────────────────
        PathGeometry geometry = GetOrBuildGeometry(conn, from, c1, c2, to);

        Color color = conn.WireColor;
        double thickness = conn.WireThickness;
        double glowThickness = emphasisLevel >= 3 ? thickness + 10 : emphasisLevel == 2 ? thickness + 9 : emphasized ? thickness + 8 : thickness + 6;
        double mainThickness = emphasisLevel >= 2 ? thickness + 1.2 : emphasized ? thickness + 1.0 : thickness;
        double glowOpacity = emphasisLevel >= 3 ? 0.72 : emphasisLevel == 2 ? 0.62 : emphasized ? 0.55 : 0.4;
        double endpointRadius = emphasisLevel >= 2 ? 4.6 : emphasized ? 4.2 : 3.5;

        // Glow pass (thick, low-alpha)
        var glowColor = Color.FromArgb(25, color.R, color.G, color.B);
        Pen glowPen = GetPen(glowColor, glowThickness, isGlow: true);
        using (dc.PushOpacity(glowOpacity))
            dc.DrawGeometry(null, glowPen, geometry);

        // Main wire
        Color boosted = BoostForEmphasis(color, emphasisLevel);
        byte mainAlpha = (byte)(emphasisLevel >= 3 ? 255 : emphasisLevel == 2 ? 240 : emphasized ? 230 : 190);
        var mainColor  = Color.FromArgb(mainAlpha, boosted.R, boosted.G, boosted.B);
        Pen mainPen    = GetPen(mainColor, mainThickness, conn.DashKind);
        dc.DrawGeometry(null, mainPen, geometry);

        // Endpoint dots
        DrawEndpointDot(dc, from, boosted, radius: endpointRadius);
        DrawEndpointDot(dc, to,   boosted, radius: endpointRadius);
    }

    private PathGeometry GetOrBuildGeometry(
        ConnectionViewModel conn,
        Point from, Point c1, Point c2, Point to
    )
    {
        var ends = new WireEndpoints(from, to);

        if (_geomCache.TryGetValue(conn, out var cached) && cached.Ends == ends)
            return cached.Geom; // endpoints unchanged — reuse

        PathGeometry geom = BuildBezier(from, c1, c2, to);
        _geomCache[conn] = (geom, ends);
        return geom;
    }

    private void DrawWireDragging(DrawingContext dc, ConnectionViewModel conn)
    {
        Color color = conn.WireColor;
        Point from = conn.FromPoint;
        Point to   = conn.ToPoint;
        (Point c1, Point c2) = BezierControlPoints(from, to);
        PathGeometry geometry = BuildBezier(from, c1, c2, to);

        // Dashed animated-looking wire for pending connections
        using (dc.PushOpacity(0.7))
            dc.DrawGeometry(null, GetDragPen(color), geometry);

        DrawEndpointDot(dc, from, color, radius: 4);
    }

    private void DrawRemovalFlashes(DrawingContext dc, Rect viewport)
    {
        if (_removalFlashes.Count == 0)
            return;

        long now = Environment.TickCount64;
        bool hasActive = false;

        for (int i = _removalFlashes.Count - 1; i >= 0; i--)
        {
            RemovalFlash flash = _removalFlashes[i];
            double t = (now - flash.StartedAtMs) / (double)RemovalFlashDurationMs;
            if (t >= 1)
            {
                _removalFlashes.RemoveAt(i);
                continue;
            }

            hasActive = true;

            Point from = flash.From;
            Point to = flash.To;
            (Point c1, Point c2) = BezierControlPoints(from, to);

            double minX = Math.Min(Math.Min(from.X, to.X), Math.Min(c1.X, c2.X));
            double minY = Math.Min(Math.Min(from.Y, to.Y), Math.Min(c1.Y, c2.Y));
            double maxX = Math.Max(Math.Max(from.X, to.X), Math.Max(c1.X, c2.X));
            double maxY = Math.Max(Math.Max(from.Y, to.Y), Math.Max(c1.Y, c2.Y));
            if (maxX < 0 || minX > viewport.Width || maxY < 0 || minY > viewport.Height)
                continue;

            PathGeometry geometry = BuildBezier(from, c1, c2, to);
            double ease = (1 - t) * (1 - t);
            byte alpha = (byte)(220 * ease);
            Color color = Color.FromArgb(alpha, flash.Color.R, flash.Color.G, flash.Color.B);
            Pen pen = GetPen(color, flash.Thickness + 2.2, flash.DashKind);

            using (dc.PushOpacity(0.95))
                dc.DrawGeometry(null, pen, geometry);
        }

        if (hasActive)
            Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
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
            IsClosed   = false,
            IsFilled   = false,
            Segments   = [seg],
        };

        return new PathGeometry { Figures = [figure] };
    }

    private static bool IsPointNearBezier(Point p, Point from, Point to, double tolerance)
    {
        (Point c1, Point c2) = BezierControlPoints(from, to);
        double tolSq = tolerance * tolerance;

        Point prev = from;
        const int segments = 24;
        for (int i = 1; i <= segments; i++)
        {
            double t = i / (double)segments;
            Point cur = EvaluateCubicBezier(from, c1, c2, to, t);
            if (DistanceSqPointToSegment(p, prev, cur) <= tolSq)
                return true;
            prev = cur;
        }

        return false;
    }

    private static Point EvaluateCubicBezier(Point p0, Point p1, Point p2, Point p3, double t)
    {
        double u = 1 - t;
        double tt = t * t;
        double uu = u * u;
        double uuu = uu * u;
        double ttt = tt * t;

        double x = (uuu * p0.X) + (3 * uu * t * p1.X) + (3 * u * tt * p2.X) + (ttt * p3.X);
        double y = (uuu * p0.Y) + (3 * uu * t * p1.Y) + (3 * u * tt * p2.Y) + (ttt * p3.Y);
        return new Point(x, y);
    }

    private static double DistanceSqPointToSegment(Point p, Point a, Point b)
    {
        double vx = b.X - a.X;
        double vy = b.Y - a.Y;
        double wx = p.X - a.X;
        double wy = p.Y - a.Y;

        double lenSq = (vx * vx) + (vy * vy);
        if (lenSq <= double.Epsilon)
            return (wx * wx) + (wy * wy);

        double t = ((wx * vx) + (wy * vy)) / lenSq;
        t = Math.Clamp(t, 0, 1);

        double px = a.X + (t * vx);
        double py = a.Y + (t * vy);
        double dx = p.X - px;
        double dy = p.Y - py;
        return (dx * dx) + (dy * dy);
    }

    private void DrawEndpointDot(DrawingContext dc, Point center, Color color, double radius)
    {
        // DotBgBrush is static — no allocation per call
        dc.DrawEllipse(DotBgBrush, null, center, radius + 1.5, radius + 1.5);
        dc.DrawEllipse(GetSolidBrush(color), null, center, radius, radius);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// DOT-GRID BACKGROUND
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Renders the infinite dot-grid background that pans and zooms with the canvas.
///
/// Performance: brushes and pens are static — allocated once for the process lifetime.
/// </summary>
public sealed class DotGridBackground : Control
{
    private const int MaxDotsPerFrame = 14000;

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

    // ── Cached instance resources (no allocations per render) ─────────────────

    private readonly SolidColorBrush _bgBrush = new(Color.Parse("#0D0F14"));
    private readonly SolidColorBrush _dotBrush = new(Color.Parse("#1E2330"));
    private readonly SolidColorBrush _majorBrush = new(Color.Parse("#161A22"));
    private readonly Pen _majorPen;

    public DotGridBackground()
    {
        _majorPen = new Pen(_majorBrush, 0.5);
    }

    public override void Render(DrawingContext dc)
    {
        // Base background fill
        dc.FillRectangle(_bgBrush, new Rect(Bounds.Size));

        const double baseSpacing = 28;
        double spacing = baseSpacing * Zoom;
        if (spacing < 6)
            return; // skip dots when too small

        double dotRadius = Math.Max(0.8, 1.2 * Zoom);

        int dotStride = ComputeDotStride(Bounds.Width, Bounds.Height, spacing, MaxDotsPerFrame);
        double effectiveSpacing = spacing * dotStride;

        // Offset so dots pan with the canvas
        double offsetX = PanOffset.X % effectiveSpacing;
        double offsetY = PanOffset.Y % effectiveSpacing;

        for (double x = offsetX; x < Bounds.Width + effectiveSpacing; x += effectiveSpacing)
        for (double y = offsetY; y < Bounds.Height + effectiveSpacing; y += effectiveSpacing)
            dc.FillRectangle(
                _dotBrush,
                new Rect(x - dotRadius, y - dotRadius, dotRadius * 2, dotRadius * 2)
            );

        // Subtle major grid lines every 5 cells
        double majorSpacing = spacing * 5;

        double majorOffX = PanOffset.X % majorSpacing;
        double majorOffY = PanOffset.Y % majorSpacing;

        for (double x = majorOffX; x < Bounds.Width + majorSpacing; x += majorSpacing)
            dc.DrawLine(_majorPen, new Point(x, 0), new Point(x, Bounds.Height));
        for (double y = majorOffY; y < Bounds.Height + majorSpacing; y += majorSpacing)
            dc.DrawLine(_majorPen, new Point(0, y), new Point(Bounds.Width, y));
    }

    private static int ComputeDotStride(double width, double height, double spacing, int maxDots)
    {
        if (spacing <= 0 || width <= 0 || height <= 0 || maxDots <= 0)
            return 1;

        double projected = ((width / spacing) + 1) * ((height / spacing) + 1);
        if (projected <= maxDots)
            return 1;

        return Math.Max(1, (int)Math.Ceiling(Math.Sqrt(projected / maxDots)));
    }
}
