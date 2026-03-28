using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using VisualSqlArchitect.UI.ViewModels.UndoRedo.Commands;

namespace VisualSqlArchitect.UI.ViewModels.Canvas;

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

    /// <summary>Grid size used for snap-to-grid (in canvas units).</summary>
    public const int GridSize = 16;

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
    /// Fits the entire canvas to the screen view.
    /// </summary>
    private void FitToScreen()
    {
        if (_canvasViewModel.Nodes.Count == 0)
            return;
        Zoom = 0.85;
        PanOffset = new Point(80, 80);
    }

    /// <summary>
    /// Rounds a value to the nearest multiple of <see cref="GridSize"/>.
    /// Call when SnapToGrid is enabled to keep nodes on the grid.
    /// </summary>
    public static double Snap(double v) => Math.Round(v / GridSize) * GridSize;
}
