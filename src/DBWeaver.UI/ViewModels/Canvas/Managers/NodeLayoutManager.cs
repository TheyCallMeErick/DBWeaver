using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using DBWeaver.UI.ViewModels.UndoRedo.Commands;

namespace DBWeaver.UI.ViewModels.Canvas;

/// <summary>
/// Manages canvas view and layout operations: zoom, pan, snap-to-grid, and auto-layout.
/// Handles viewport transformation and node positioning logic.
/// </summary>
public sealed class NodeLayoutManager : ViewModelBase
{
    private double _zoom = 1.0;
    private Point _panOffset;
    private bool _snapToGrid = true;
    private readonly CanvasViewModel _canvasViewModel;
    private readonly UndoRedoStack _undoRedo;

    // Viewport size — updated by InfiniteCanvas after each ArrangeOverride pass.
    // Defaults to a reasonable fallback matching the minimum window size.
    private double _viewportWidth = 1200;
    private double _viewportHeight = 800;

    // Estimated node dimensions used when the real size has not been measured yet.
    private const double FallbackNodeWidth = 230;
    private const double FallbackNodeHeight = 130;
    private const double FitPadding = 60; // canvas units of space around the content

    /// <summary>Grid size used for snap-to-grid (in canvas units).</summary>
    public const int GridSize = 16;

    private const double LayoutNodeWidth = 230;
    private const double LayoutNodeHeight = 130;
    private const double LayoutColGap = 80;
    private const double LayoutRowGap = 40;

    public double Zoom
    {
        get => _zoom;
        set
        {
            double clamped = Math.Clamp(value, 0.15, 4.0);
            if (Set(ref _zoom, clamped))
                RaisePropertyChanged(nameof(ZoomPercent));
        }
    }

    public Point PanOffset
    {
        get => _panOffset;
        set => Set(ref _panOffset, value);
    }

    public bool SnapToGrid
    {
        get => _snapToGrid;
        set
        {
            if (Set(ref _snapToGrid, value))
                RaisePropertyChanged(nameof(SnapToGridLabel));
        }
    }

    public string ZoomPercent => $"{Zoom * 100:F0}%";
    public string SnapToGridLabel => _snapToGrid ? "Snap ON" : "Snap OFF";

    public RelayCommand ZoomInCommand { get; }
    public RelayCommand ZoomOutCommand { get; }
    public RelayCommand ResetZoomCommand { get; }
    public RelayCommand FitToScreenCommand { get; }
    public RelayCommand ToggleSnapCommand { get; }

    public NodeLayoutManager(CanvasViewModel canvasViewModel, UndoRedoStack undoRedo)
    {
        _canvasViewModel = canvasViewModel;
        _undoRedo = undoRedo;

        ZoomInCommand = new RelayCommand(() => Zoom *= 1.15);
        ZoomOutCommand = new RelayCommand(() => Zoom /= 1.15);
        ResetZoomCommand = new RelayCommand(() =>
        {
            Zoom = 1.0;
            PanOffset = new Point(0, 0);
        });
        FitToScreenCommand = new RelayCommand(FitToScreen);
        ToggleSnapCommand = new RelayCommand(() => SnapToGrid = !SnapToGrid);
    }

    /// <summary>
    /// Arranges all nodes into logical columns (DataSources → Transforms → Outputs).
    /// The operation is undoable via Ctrl+Z.
    /// Optionally pass a scope to layout only selected nodes.
    /// </summary>
    public void RunAutoLayout(IReadOnlyList<NodeViewModel>? scope = null)
    {
        if (_canvasViewModel.Nodes.Count == 0)
            return;
        var cmd = new AutoLayoutCommand(_canvasViewModel, scope);
        _undoRedo.Execute(cmd);
    }

    /// <summary>
    /// Zooms toward a specific screen point with the given factor.
    /// Keeps the point under the cursor stationary while zooming.
    /// </summary>
    public void ZoomToward(Point screen, double factor)
    {
        double old = Zoom;
        Zoom = Math.Clamp(old * factor, 0.15, 4.0);
        PanOffset = new Point(
            screen.X - (screen.X - PanOffset.X) * (Zoom / old),
            screen.Y - (screen.Y - PanOffset.Y) * (Zoom / old)
        );
    }

    /// <summary>
    /// Converts screen coordinates to canvas coordinates.
    /// </summary>
    public Point ScreenToCanvas(Point s) =>
        new((s.X - PanOffset.X) / Zoom, (s.Y - PanOffset.Y) / Zoom);

    /// <summary>
    /// Converts canvas coordinates to screen coordinates.
    /// </summary>
    public Point CanvasToScreen(Point c) => new(c.X * Zoom + PanOffset.X, c.Y * Zoom + PanOffset.Y);

    /// <summary>
    /// Returns the center of the current visible viewport in canvas coordinates.
    /// Useful for spawning new nodes where the user is currently looking.
    /// </summary>
    public Point ViewportCenterCanvas() =>
        ScreenToCanvas(new Point(_viewportWidth / 2.0, _viewportHeight / 2.0));

    /// <summary>
    /// Updates the stored viewport size so that <see cref="FitToScreen"/> can compute
    /// an accurate zoom level.  Called by <c>InfiniteCanvas.ArrangeOverride</c>.
    /// </summary>
    public void SetViewportSize(double width, double height)
    {
        if (width > 0) _viewportWidth = width;
        if (height > 0) _viewportHeight = height;
    }

    /// <summary>
    /// Centers the viewport around a specific node, keeping current zoom unless it is too far out.
    /// </summary>
    public void FocusOnNode(NodeViewModel node)
    {
        if (node is null)
            return;

        if (Zoom < 0.75)
            Zoom = 0.75;

        double width = node.Width > 0 ? node.Width : FallbackNodeWidth;
        double nodeCenterX = node.Position.X + width / 2.0;
        double nodeCenterY = node.Position.Y + FallbackNodeHeight / 2.0;

        PanOffset = new Point(
            _viewportWidth / 2.0 - nodeCenterX * Zoom,
            _viewportHeight / 2.0 - nodeCenterY * Zoom
        );
    }

    /// <summary>
    /// Fits all nodes into the current viewport by computing the real bounding box
    /// and deriving an appropriate zoom level and pan offset.
    /// Falls back to no-op when there are no nodes.
    /// </summary>
    private void FitToScreen()
    {
        if (_canvasViewModel.Nodes.Count == 0)
            return;

        // ── 1. Compute bounding box of all nodes ────────────────────────────
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (NodeViewModel node in _canvasViewModel.Nodes)
        {
            double w = node.Width > 0 ? node.Width : FallbackNodeWidth;
            double x = node.Position.X;
            double y = node.Position.Y;

            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x + w);
            maxY = Math.Max(maxY, y + FallbackNodeHeight);
        }

        // ── 2. Compute zoom to fit content (with padding) into the viewport ──
        double contentW = maxX - minX + FitPadding * 2;
        double contentH = maxY - minY + FitPadding * 2;

        double zoomX = _viewportWidth / contentW;
        double zoomY = _viewportHeight / contentH;
        Zoom = Math.Clamp(Math.Min(zoomX, zoomY), 0.15, 2.0);

        // ── 3. Center the content inside the viewport ────────────────────────
        double scaledCenterX = (minX + (maxX - minX) / 2.0) * Zoom;
        double scaledCenterY = (minY + (maxY - minY) / 2.0) * Zoom;
        PanOffset = new Point(
            _viewportWidth / 2.0 - scaledCenterX,
            _viewportHeight / 2.0 - scaledCenterY
        );
    }

    /// <summary>
    /// Rounds a value to the nearest multiple of <see cref="GridSize"/>.
    /// Call when SnapToGrid is enabled to keep nodes on the grid.
    /// </summary>
    public static double Snap(double v) => Math.Round(v / GridSize) * GridSize;

    public static Dictionary<NodeViewModel, Point> ComputeAutoLayout(
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections,
        Func<NodeViewModel, bool>? isSinkNode = null,
        Point? origin = null
    )
    {
        if (nodes.Count == 0)
            return [];

        Point start = origin ?? new Point(60, 60);

        var forward = nodes.ToDictionary(n => n, _ => new HashSet<NodeViewModel>());
        var backward = nodes.ToDictionary(n => n, _ => new HashSet<NodeViewModel>());

        foreach (ConnectionViewModel conn in connections)
        {
            NodeViewModel from = conn.FromPin.Owner;
            NodeViewModel? to = conn.ToPin?.Owner;
            if (to is null || !forward.ContainsKey(from) || !forward.ContainsKey(to))
                continue;

            forward[from].Add(to);
            backward[to].Add(from);
        }

        var layer = new Dictionary<NodeViewModel, int>();
        var inDegree = nodes.ToDictionary(n => n, n => backward[n].Count);
        var queue = new Queue<NodeViewModel>(nodes.Where(n => inDegree[n] == 0));

        while (queue.Count > 0)
        {
            NodeViewModel node = queue.Dequeue();
            layer[node] =
                backward[node].Count == 0
                    ? 0
                    : backward[node].Max(p => layer.TryGetValue(p, out int l) ? l : 0) + 1;

            foreach (NodeViewModel next in forward[node])
            {
                inDegree[next]--;
                if (inDegree[next] == 0)
                    queue.Enqueue(next);
            }
        }

        foreach (NodeViewModel node in nodes.Where(n => !layer.ContainsKey(n)))
            layer[node] = 0;

        if (isSinkNode is not null)
        {
            int maxLayer = layer.Values.DefaultIfEmpty(0).Max();
            int sinkLayer = maxLayer + 1;
            foreach (NodeViewModel node in nodes.Where(isSinkNode))
                layer[node] = sinkLayer;
        }

        int finalMaxLayer = layer.Values.DefaultIfEmpty(0).Max();

        var byLayer = Enumerable
            .Range(0, finalMaxLayer + 1)
            .ToDictionary(
                l => l,
                l =>
                    nodes
                        .Where(n => layer[n] == l)
                        .OrderBy(n =>
                            backward[n].Count == 0
                                ? n.Position.Y
                                : backward[n].Average(p => p.Position.Y)
                        )
                        .ToList()
            );

        var result = new Dictionary<NodeViewModel, Point>();
        for (int col = 0; col <= finalMaxLayer; col++)
        {
            List<NodeViewModel> nodesInCol = byLayer[col];
            double x = start.X + col * (LayoutNodeWidth + LayoutColGap);

            for (int row = 0; row < nodesInCol.Count; row++)
            {
                double y = start.Y + row * (LayoutNodeHeight + LayoutRowGap);
                result[nodesInCol[row]] = new Point(x, y);
            }
        }

        return result;
    }
}
