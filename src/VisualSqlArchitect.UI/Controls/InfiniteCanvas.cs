using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.UndoRedo.Commands;

namespace VisualSqlArchitect.UI.Controls;

public sealed class InfiniteCanvas : Panel
{
    public static readonly StyledProperty<CanvasViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<InfiniteCanvas, CanvasViewModel?>(nameof(ViewModel));

    public CanvasViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    static InfiniteCanvas()
    {
        ViewModelProperty.Changed.AddClassHandler<InfiniteCanvas>((c, _) => c.Rebuild());
        ClipToBoundsProperty.OverrideDefaultValue<InfiniteCanvas>(true);
    }

    private readonly DotGridBackground _grid = new() { IsHitTestVisible = false };
    private readonly Canvas _scene = new();
    private readonly BezierWireLayer _wires = new();

    private PinDragInteraction? _pinDrag;
    private double _zoom = 1.0;
    private Point _panOffset;
    private bool _isPanning;
    private Point _panStart;
    private bool _isRubberBanding;
    private Point _rubberStart,
        _rubberCurrent;
    private NodeViewModel? _dragNode;
    private Point _nodeDragStart,
        _nodePosStart;

    // Alignment guides drawn during node drag
    private readonly AlignGuidesLayer _guides = new() { IsHitTestVisible = false };

    // O(1) lookup: NodeViewModel → its NodeControl (maintained in SyncNodes)
    private readonly Dictionary<NodeViewModel, NodeControl> _nodeControlCache = new();
    // Per-node PropertyChanged handlers so they can be unsubscribed on removal
    private readonly Dictionary<NodeViewModel, PropertyChangedEventHandler> _nodePositionHandlers = new();

    // Preserve camera state during node drag to prevent reset during snapping operations
    private bool _isNodeDragging;
    private double _dragStartZoom;
    private Point _dragStartPanOffset;

    // Skip guide recalculation when the node hasn't moved more than this threshold (canvas units)
    private Point _lastGuideCheckPosition;
    private const double GuideRecheckThresholdSq = 4.0; // 2px²

    public InfiniteCanvas()
    {
        // Transparent background makes the entire panel surface hit-testable,
        // so middle-mouse panning and rubber-band work even on empty canvas areas.
        Background = Brushes.Transparent;
        Children.Add(_grid);
        Children.Add(_scene);
        _scene.Children.Add(_wires);
        Children.Add(_guides);
        PointerWheelChanged += OnWheel;
        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
        KeyDown += OnKey;
        Focusable = true;
        // Re-sync wire endpoints after every layout pass so that pin positions
        // are computed from the real nc.Bounds.Width, not the 220px fallback
        // that is used when NodeControls have not yet been measured/arranged.
        LayoutUpdated += (_, _) =>
        {
            if (ViewModel is not null)
                SyncWires();
        };
    }

    protected override Size MeasureOverride(Size s)
    {
        _grid.Measure(s);
        _scene.Measure(Size.Infinity);
        return s;
    }

    protected override Size ArrangeOverride(Size s)
    {
        _grid.Arrange(new Rect(s));
        _grid.Width = s.Width;
        _grid.Height = s.Height;
        _grid.Zoom = _zoom;
        _grid.PanOffset = _panOffset;

        _scene.RenderTransform = new TransformGroup
        {
            Children =
            [
                new ScaleTransform(_zoom, _zoom),
                new TranslateTransform(_panOffset.X, _panOffset.Y),
            ],
        };
        _scene.Arrange(
            new Rect(
                new Point(-_panOffset.X / _zoom, -_panOffset.Y / _zoom),
                new Size(20000, 20000)
            )
        );

        foreach (NodeControl nc in _scene.Children.OfType<NodeControl>())
            if (nc.DataContext is NodeViewModel vm)
            {
                Canvas.SetLeft(nc, vm.Position.X);
                Canvas.SetTop(nc, vm.Position.Y);
                nc.Measure(Size.Infinity);
                nc.Arrange(new Rect(new Point(vm.Position.X, vm.Position.Y), nc.DesiredSize));
            }

        _wires.Arrange(new Rect(new Size(20000, 20000)));
        _guides.Arrange(new Rect(s));
        _guides.Width = s.Width;
        _guides.Height = s.Height;
        _guides.Zoom = _zoom;
        _guides.PanOffset = _panOffset;
        return s;
    }

    private void Rebuild()
    {
        foreach (NodeControl? nc in _scene.Children.OfType<NodeControl>().ToList())
            _scene.Children.Remove(nc);
        _nodeControlCache.Clear();
        _nodePositionHandlers.Clear();
        if (ViewModel is null)
            return;
        _pinDrag = new PinDragInteraction(ViewModel, _scene);
        ViewModel.Nodes.CollectionChanged += (_, _) => SyncNodes();
        ViewModel.Connections.CollectionChanged += (_, _) => SyncWires();
        // Only sync Zoom from ViewModel, NOT PanOffset (canvas manages pan independently)
        // Skip this during node drag to prevent camera reset when snapping triggers property changes
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CanvasViewModel.Zoom) && !_isNodeDragging)
                SyncTransform();
        };
        // Initialize from ViewModel (load from persistence)
        _panOffset = ViewModel.PanOffset;
        _zoom = ViewModel.Zoom;
        SyncNodes();
        SyncWires();
        SyncTransform();
    }

    private void SyncNodes()
    {
        if (ViewModel is null)
            return;

        // Add controls for nodes not yet in the cache
        foreach (NodeViewModel vm in ViewModel.Nodes)
        {
            if (_nodeControlCache.ContainsKey(vm))
                continue;
            var nc = new NodeControl { DataContext = vm };
            Canvas.SetLeft(nc, vm.Position.X);
            Canvas.SetTop(nc, vm.Position.Y);
            nc.NodeDragStarted += OnNodeDragStarted;
            nc.NodeDragDelta += OnNodeDragDelta;
            nc.NodeDragCompleted += OnNodeDragCompleted;
            nc.NodeClicked += OnNodeClicked;
            nc.PinPressed += OnPinPressed;

            // Track position handler so it can be unsubscribed when the node is removed
            NodeControl capturedNc = nc;
            NodeViewModel capturedVm = vm;
            PropertyChangedEventHandler posHandler = (_, e) =>
            {
                if (e.PropertyName == nameof(NodeViewModel.Position))
                {
                    Canvas.SetLeft(capturedNc, capturedVm.Position.X);
                    Canvas.SetTop(capturedNc, capturedVm.Position.Y);
                    SyncWires();
                    InvalidateArrange();
                }
            };
            vm.PropertyChanged += posHandler;
            _nodePositionHandlers[vm] = posHandler;
            _nodeControlCache[vm] = nc;
            _scene.Children.Add(nc);
        }

        // Remove controls for nodes no longer in ViewModel.Nodes
        var currentNodes = new HashSet<NodeViewModel>(ViewModel.Nodes);
        var toRemove = _nodeControlCache.Keys.Where(vm => !currentNodes.Contains(vm)).ToList();
        foreach (NodeViewModel vm in toRemove)
        {
            NodeControl nc = _nodeControlCache[vm];
            nc.NodeDragStarted -= OnNodeDragStarted;
            nc.NodeDragDelta -= OnNodeDragDelta;
            nc.NodeDragCompleted -= OnNodeDragCompleted;
            nc.NodeClicked -= OnNodeClicked;
            nc.PinPressed -= OnPinPressed;
            if (_nodePositionHandlers.TryGetValue(vm, out PropertyChangedEventHandler? posHandler))
            {
                vm.PropertyChanged -= posHandler;
                _nodePositionHandlers.Remove(vm);
            }
            _nodeControlCache.Remove(vm);
            _scene.Children.Remove(nc);
        }
    }

    private void SyncWires()
    {
        if (ViewModel is null)
            return;
        UpdatePinPositions();
        foreach (ConnectionViewModel c in ViewModel.Connections)
        {
            c.FromPoint = c.FromPin.AbsolutePosition;
            if (c.ToPin is not null)
                c.ToPoint = c.ToPin.AbsolutePosition;
        }
        _wires.Connections = ViewModel.Connections;
        _wires.PendingConnection = _pinDrag?.LiveWire;
        _wires.InvalidateVisual();
    }

    public void InvalidateWires() => SyncWires();

    private void SyncTransform()
    {
        if (ViewModel is null)
            return;
        _zoom = ViewModel.Zoom;
        InvalidateArrange();
        _grid.Zoom = _zoom;
        _grid.PanOffset = _panOffset;
        _grid.InvalidateVisual();
    }

    private void UpdatePinPositions()
    {
        if (ViewModel is null)
            return;
        foreach (NodeViewModel node in ViewModel.Nodes)
        {
            if (!_nodeControlCache.TryGetValue(node, out NodeControl? nc))
                continue;

            // CRITICAL: Force re-measure/arrange to ensure pins have correct coordinates
            // This is essential for TranslatePoint to return accurate positions
            nc.Measure(Size.Infinity);
            nc.Arrange(new Rect(new Point(node.Position.X, node.Position.Y), nc.DesiredSize));

            // After forced layout: use TranslatePoint from each pin Border for pixel-accurate positions.
            foreach (Border b in nc.GetLogicalDescendants().OfType<Border>())
            {
                if (b.DataContext is not PinViewModel pvm)
                    continue;
                if (b.Bounds.Width == 0)
                    continue; // skip collapsed/hidden elements
                var center = new Point(b.Bounds.Width / 2, b.Bounds.Height / 2);
                Point? inScene = b.TranslatePoint(center, _scene);
                if (inScene.HasValue)
                {
                    // Only update if it's a reasonable coordinate (not NaN or extremely far away)
                    if (!double.IsNaN(inScene.Value.X) && !double.IsNaN(inScene.Value.Y) &&
                        !double.IsInfinity(inScene.Value.X) && !double.IsInfinity(inScene.Value.Y))
                    {
                        pvm.AbsolutePosition = inScene.Value;
                    }
                }
            }
        }
    }

    private void OnNodeClicked(object? s, (NodeViewModel Node, bool Shift) a) =>
        ViewModel?.SelectNode(a.Node, a.Shift);

    private void OnNodeDragStarted(object? s, (NodeViewModel Node, Point Pos) a)
    {
        _dragNode = a.Node;
        _nodeDragStart = a.Pos;
        _nodePosStart = a.Node.Position;
        _lastGuideCheckPosition = a.Node.Position;

        // Preserve camera state during drag to prevent reset during snapping operations
        _isNodeDragging = true;
        _dragStartZoom = _zoom;
        _dragStartPanOffset = _panOffset;
    }

    private void OnNodeDragDelta(object? s, (NodeViewModel Node, Point Pos) a)
    {
        if (_dragNode is null)
            return;
        Point d = a.Pos - _nodeDragStart;
        double rawX = _nodePosStart.X + d.X / _zoom;
        double rawY = _nodePosStart.Y + d.Y / _zoom;

        double newX = rawX,
            newY = rawY;
        if (ViewModel?.SnapToGrid == true)
        {
            newX = CanvasViewModel.Snap(rawX);
            newY = CanvasViewModel.Snap(rawY);
        }

        _dragNode.Position = new Point(newX, newY);

        // Force immediate layout update for the dragged node so wires sync with current position
        if (_nodeControlCache.TryGetValue(_dragNode, out NodeControl? nc))
        {
            nc.Measure(Size.Infinity);
            nc.Arrange(new Rect(new Point(newX, newY), nc.DesiredSize));
        }

        SyncWires();

        // Only recalculate guides when the node has moved enough (avoids O(n) every frame)
        double gdx = newX - _lastGuideCheckPosition.X;
        double gdy = newY - _lastGuideCheckPosition.Y;
        if (gdx * gdx + gdy * gdy >= GuideRecheckThresholdSq)
        {
            _lastGuideCheckPosition = _dragNode.Position;
            UpdateAlignGuides(_dragNode);
        }
    }

    private void OnNodeDragCompleted(object? s, (NodeViewModel Node, Point Pos) a)
    {
        if (_dragNode is not null && ViewModel is not null && _dragNode.Position != _nodePosStart)
        {
            Point final = _dragNode.Position;
            _dragNode.Position = _nodePosStart;
            ViewModel.UndoRedo.Execute(new MoveNodeCommand(_dragNode, _nodePosStart, final));
        }
        _dragNode = null;
        _guides.ClearGuides();

        // End drag and restore camera if it was unexpectedly changed during the drag
        _isNodeDragging = false;
        if (_zoom != _dragStartZoom || _panOffset != _dragStartPanOffset)
        {
            _zoom = _dragStartZoom;
            _panOffset = _dragStartPanOffset;
            InvalidateArrange();
        }
    }

    private void OnPinPressed(object? s, (PinViewModel Pin, Point Canvas) a)
    {
        _pinDrag?.BeginDrag(a.Pin, a.Canvas);
        _wires.PendingConnection = _pinDrag?.LiveWire;
    }

    private void OnWheel(object? s, PointerWheelEventArgs e)
    {
        if (ViewModel is null)
            return;
        ViewModel.ZoomToward(e.GetPosition(this), e.Delta.Y > 0 ? 1.10 : 0.91);
        // SyncTransform is triggered automatically via ViewModel.PropertyChanged handler.
        e.Handled = true;
    }

    private void OnPressed(object? s, PointerPressedEventArgs e)
    {
        Focus();
        PointerPointProperties props = e.GetCurrentPoint(this).Properties;
        Point screen = e.GetPosition(this);

        if (
            props.IsMiddleButtonPressed
            || (props.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        )
        {
            _isPanning = true;
            _panStart = screen;  // Store screen position, not difference
            e.Pointer.Capture(this);
            Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Handled = true;
            return;
        }

        if (props.IsLeftButtonPressed)
        {
            Point canvas = ScreenToCanvas(screen);
            // If a NodeControl already fired PinPressed (which called BeginDrag via OnPinPressed),
            // the event bubbled here without e.Handled — just capture the pointer so that
            // move and release events are owned by the canvas for the full drag gesture.
            if (_pinDrag?.IsDragging == true)
            {
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
            PinViewModel? pin = _pinDrag?.HitTestPin(canvas);
            if (pin is not null)
            {
                _pinDrag!.BeginDrag(pin, canvas);
                _wires.PendingConnection = _pinDrag.LiveWire;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
            ViewModel?.DeselectAll();
            _isRubberBanding = true;
            _rubberStart = canvas;
            _rubberCurrent = canvas;
        }

        if (props.IsRightButtonPressed)
            ShowContextMenu(screen);
    }

    private void OnMoved(object? s, PointerEventArgs e)
    {
        Point screen = e.GetPosition(this);
        Point canvas = ScreenToCanvas(screen);
        if (_isPanning)
        {
            Point delta = screen - _panStart;
            _panOffset = _panOffset + (Vector)delta;

            // Force immediate visual update during pan (don't wait for layout pass)
            _scene.RenderTransform = new TransformGroup
            {
                Children =
                [
                    new ScaleTransform(_zoom, _zoom),
                    new TranslateTransform(_panOffset.X, _panOffset.Y),
                ],
            };
            _grid.PanOffset = _panOffset;
            _grid.InvalidateVisual();

            _panStart = screen;  // Update for next frame
            return;
        }
        if (_pinDrag?.IsDragging == true)
        {
            _pinDrag.UpdateDrag(canvas);
            _wires.PendingConnection = _pinDrag.LiveWire;
            _wires.InvalidateVisual();
            return;
        }
        if (_isRubberBanding)
        {
            _rubberCurrent = canvas;
            UpdateRubberBand();
            UpdateRubberBandVisual();
        }
    }

    private void OnReleased(object? s, PointerReleasedEventArgs e)
    {
        Point canvas = ScreenToCanvas(e.GetPosition(this));
        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
            Cursor = Cursor.Default;
            return;
        }
        if (_pinDrag?.IsDragging == true)
        {
            _pinDrag.EndDrag(canvas);
            _wires.PendingConnection = null;
            _wires.InvalidateVisual();
            e.Pointer.Capture(null);
            return;
        }
        if (_isRubberBanding)
        {
            _isRubberBanding = false;
            UpdateRubberBand();
            UpdateRubberBandVisual();
        }
    }

    private void OnKey(object? s, KeyEventArgs e)
    {
        if (ViewModel is null)
            return;
        if (e.Key == Key.A && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            ViewModel.SearchMenu.Open(
                ScreenToCanvas(new Point(Bounds.Width / 2, Bounds.Height / 2))
            );
            e.Handled = true;
            return;
        }
        if (e.Key == Key.F3)
        {
            ViewModel.DataPreview.Toggle();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ViewModel.UndoRedo.Undo();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Y && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ViewModel.UndoRedo.Redo();
            e.Handled = true;
            return;
        }
        if (e.Key is Key.Delete or Key.Back)
        {
            ViewModel.DeleteSelected();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.A && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ViewModel.SelectAll();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape)
        {
            _pinDrag?.CancelDrag();
            _wires.PendingConnection = null;
            _wires.InvalidateVisual();
            ViewModel.DeselectAll();
            ViewModel.SearchMenu.Close();
            _isRubberBanding = false;
            UpdateRubberBandVisual();
            e.Handled = true;
        }
    }

    private void UpdateRubberBand()
    {
        if (ViewModel is null)
            return;
        var r = new Rect(
            Math.Min(_rubberStart.X, _rubberCurrent.X),
            Math.Min(_rubberStart.Y, _rubberCurrent.Y),
            Math.Abs(_rubberCurrent.X - _rubberStart.X),
            Math.Abs(_rubberCurrent.Y - _rubberStart.Y)
        );
        foreach (NodeViewModel n in ViewModel.Nodes)
            n.IsSelected = r.Contains(n.Position);
    }

    // Note: In Avalonia 11+, Panel.Render() is sealed and cannot be overridden.
    // Rubber band selection rectangle is drawn as a visual element instead.
    private Border? _rubberBandRect;

    private void UpdateRubberBandVisual()
    {
        if (!_isRubberBanding)
        {
            if (_rubberBandRect != null)
            {
                _scene.Children.Remove(_rubberBandRect);
                _rubberBandRect = null;
            }
            return;
        }

        double x = _panOffset.X + Math.Min(_rubberStart.X, _rubberCurrent.X) * _zoom;
        double y = _panOffset.Y + Math.Min(_rubberStart.Y, _rubberCurrent.Y) * _zoom;
        double w = Math.Abs(_rubberCurrent.X - _rubberStart.X) * _zoom;
        double h = Math.Abs(_rubberCurrent.Y - _rubberStart.Y) * _zoom;

        if (_rubberBandRect == null)
        {
            _rubberBandRect = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#103B82F6")),
                BorderBrush = new SolidColorBrush(Color.Parse("#3B82F6")),
                BorderThickness = new Thickness(1),
            };
            _scene.Children.Add(_rubberBandRect);
        }

        Canvas.SetLeft(_rubberBandRect, x);
        Canvas.SetTop(_rubberBandRect, y);
        _rubberBandRect.Width = w;
        _rubberBandRect.Height = h;
    }

    private void ShowContextMenu(Point screen)
    {
        if (ViewModel is null)
            return;
        var sel = ViewModel.Nodes.Where(n => n.IsSelected).ToList();
        var menu = new ContextMenu();
        if (sel.Count > 0)
            menu.Items.Add(
                new MenuItem
                {
                    Header = $"Delete {(sel.Count == 1 ? sel[0].Title : $"{sel.Count} nodes")}",
                    Command = new RelayCommand(ViewModel.DeleteSelected),
                }
            );
        else
            menu.Items.Add(
                new MenuItem
                {
                    Header = "Add Node (Shift+A)",
                    Command = new RelayCommand(() =>
                        ViewModel.SearchMenu.Open(ScreenToCanvas(screen))
                    ),
                }
            );
        menu.Items.Add(new Separator());
        menu.Items.Add(
            new MenuItem
            {
                Header = $"Undo {ViewModel.UndoRedo.UndoDescription}",
                Command = ViewModel.UndoCommand,
            }
        );
        menu.Items.Add(new MenuItem { Header = "Redo", Command = ViewModel.RedoCommand });
        menu.Open(this);
    }

    private Point ScreenToCanvas(Point s) =>
        new((s.X - _panOffset.X) / _zoom, (s.Y - _panOffset.Y) / _zoom);

    // ── Alignment guide detection ─────────────────────────────────────────────

    private const double GuideThreshold = 8.0; // canvas units — how close to snap a guide
    private const double DefaultNodeH = 130;

    /// <summary>
    /// Detects horizontal and vertical alignment between the dragged node and all
    /// other (non-selected) nodes.  Guides appear for: left edge, right edge,
    /// vertical centre, top edge, bottom edge, horizontal centre.
    /// </summary>
    private void UpdateAlignGuides(NodeViewModel dragged)
    {
        if (ViewModel is null)
        {
            _guides.ClearGuides();
            return;
        }

        var others = ViewModel.Nodes.Where(n => n != dragged && !n.IsSelected).ToList();

        double dX = dragged.Position.X;
        double dW = dragged.Width > 0 ? dragged.Width : 230;
        double dCX = dX + dW / 2.0;
        double dRX = dX + dW;

        double dY = dragged.Position.Y;
        double dH = DefaultNodeH;
        double dCY = dY + dH / 2.0;
        double dBY = dY + dH;

        var hGuides = new List<double>(); // Y coordinates of horizontal guides
        var vGuides = new List<double>(); // X coordinates of vertical guides

        foreach (NodeViewModel? n in others)
        {
            double oX = n.Position.X;
            double oW = n.Width > 0 ? n.Width : 230;
            double oCX = oX + oW / 2.0;
            double oRX = oX + oW;

            double oY = n.Position.Y;
            double oH = DefaultNodeH;
            double oCY = oY + oH / 2.0;
            double oBY = oY + oH;

            // Vertical guides (X alignment)
            if (Math.Abs(dX - oX) < GuideThreshold)
                vGuides.Add(oX);
            if (Math.Abs(dX - oRX) < GuideThreshold)
                vGuides.Add(oRX);
            if (Math.Abs(dCX - oCX) < GuideThreshold)
                vGuides.Add(oCX);
            if (Math.Abs(dRX - oX) < GuideThreshold)
                vGuides.Add(oX);
            if (Math.Abs(dRX - oRX) < GuideThreshold)
                vGuides.Add(oRX);

            // Horizontal guides (Y alignment)
            if (Math.Abs(dY - oY) < GuideThreshold)
                hGuides.Add(oY);
            if (Math.Abs(dY - oBY) < GuideThreshold)
                hGuides.Add(oBY);
            if (Math.Abs(dCY - oCY) < GuideThreshold)
                hGuides.Add(oCY);
            if (Math.Abs(dBY - oY) < GuideThreshold)
                hGuides.Add(oY);
            if (Math.Abs(dBY - oBY) < GuideThreshold)
                hGuides.Add(oBY);
        }

        _guides.SetGuides([.. hGuides.Distinct()], [.. vGuides.Distinct()]);
    }
}

