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

public sealed partial class InfiniteCanvas : Panel
{
    private static void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        Console.WriteLine($"[{timestamp}] [InfiniteCanvas] {message}");
    }

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
    private bool _isApplyingViewportFromCanvas;
    private bool _isSpacePanArmed;
    private bool _contextMenuPending;
    private Point _contextMenuPressScreen;
    private const double ContextPanStartThreshold = 4.0;

    internal bool IsSpacePanModeArmed => _isSpacePanArmed;

    // NodifyM pattern: Track pan offset at start of operations for compensation during node drag
    private Point _startPanOffset;
    private Point _startNodeDragCanvasPos;

    private bool _isRubberBanding;
    private Point _rubberStart,
        _rubberCurrent;
    private const bool RubberBandEnabled = false;
    private NodeViewModel? _dragNode;
    private Point _nodeDragStart,
        _nodePosStart;

    // Multi-node group drag: other selected nodes moving with _dragNode
    private List<(NodeViewModel Node, Point StartPos)>? _groupDragStarts;

    // Track whether we've initialized from ViewModel to prevent resetting pan offset during rebuild
    private bool _hasInitialized;

    // Alignment guides drawn during node drag
    private readonly AlignGuidesLayer _guides = new() { IsHitTestVisible = false };

    // O(1) lookup: NodeViewModel → its NodeControl (maintained in SyncNodes)
    private readonly Dictionary<NodeViewModel, NodeControl> _nodeControlCache = new();
    // Per-node PropertyChanged handlers so they can be unsubscribed on removal
    private readonly Dictionary<NodeViewModel, PropertyChangedEventHandler> _nodePositionHandlers = new();

    // Skip guide recalculation when the node hasn't moved more than this threshold (canvas units)
    private Point _lastGuideCheckPosition;
    private const double GuideRecheckThresholdSq = 4.0; // 2px²
    private bool _wireSyncQueued;

    public InfiniteCanvas()
    {
        // Transparent background makes the entire panel surface hit-testable,
        // so middle-mouse panning and rubber-band work even on empty canvas areas.
        Background = Brushes.Transparent;
        _scene.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
        Children.Add(_grid);
        Children.Add(_scene);
        _scene.Children.Add(_wires);
        Children.Add(_guides);
        PointerWheelChanged += OnWheel;
        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
        KeyDown += OnKey;
        KeyUp += OnKeyUp;
        Focusable = true;
        // Re-sync wire endpoints after every layout pass so that pin positions
        // are computed from the real nc.Bounds.Width, not the 220px fallback
        // that is used when NodeControls have not yet been measured/arranged.
        LayoutUpdated += (_, _) =>
        {
            if (ViewModel is not null)
                RequestWireSync();
        };
    }

    private void RequestWireSync()
    {
        if (ViewModel is null || _wireSyncQueued)
            return;

        _wireSyncQueued = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(
            () =>
            {
                _wireSyncQueued = false;
                if (ViewModel is not null)
                    SyncWires();
            },
            Avalonia.Threading.DispatcherPriority.Render
        );
    }

    protected override Size MeasureOverride(Size s)
    {
        _grid.Measure(s);
        _scene.Measure(Size.Infinity);
        Log($"    MeasureOverride: Size={s}, PanOffset={_panOffset}");
        return s;
    }

    protected override Size ArrangeOverride(Size s)
    {
        try
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
                    // Keep scene arranged at origin; pan/zoom must be applied only via RenderTransform.
                    // Applying pan both in Arrange origin and TranslateTransform causes visual reset on layout passes.
                    new Point(0, 0),
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

            // Keep FitToScreen accurate by letting the layout manager know the viewport size.
            ViewModel?.SetViewportSize(s.Width, s.Height);

            // DETECT UNEXPECTED PAN RESETS
            if (_panOffset.X == 0 && _panOffset.Y == 0 && _zoom == 1 && !_isPanning && _dragNode is null)
            {
                Log($"    [ArrangeOverride] Current state: PanOffset={_panOffset}, Zoom={_zoom}, Panning={_isPanning}, Dragging={_dragNode is not null}");
            }

            return s;
        }
        catch (Exception ex)
        {
            Log($"!!! ERROR in ArrangeOverride: {ex.GetType().Name}: {ex.Message}");
            Log($"    StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    private void Rebuild()
    {
        try
        {
            Log($">>> REBUILD started");
            foreach (NodeControl? nc in _scene.Children.OfType<NodeControl>().ToList())
                _scene.Children.Remove(nc);
            _nodeControlCache.Clear();
            _nodePositionHandlers.Clear();
            if (ViewModel is null)
            {
                Log($"    ViewModel is null, exiting rebuild");
                return;
            }
            _pinDrag = new PinDragInteraction(ViewModel, _scene);
            ViewModel.Nodes.CollectionChanged += (_, e) =>
            {
                Log($"    !!! ViewModel.Nodes.CollectionChanged: Action={e.Action}");
                SyncNodes();
            };
            ViewModel.Connections.CollectionChanged += (_, e) =>
            {
                Log($"    !!! ViewModel.Connections.CollectionChanged: Action={e.Action}");
                RequestWireSync();
            };
            // Keep canvas transform in sync with ViewModel for both Zoom and PanOffset.
            ViewModel.PropertyChanged += (_, e) =>
            {
                if (_isApplyingViewportFromCanvas)
                    return;

                if (e.PropertyName == nameof(CanvasViewModel.Zoom))
                {
                    Log($"    !!! ViewModel.Zoom changed, calling SyncTransform");
                    SyncTransform();
                }
                else if (e.PropertyName == nameof(CanvasViewModel.PanOffset))
                {
                    Log($"    !!! ViewModel.PanOffset changed, calling SyncTransform");
                    SyncTransform();
                }
            };
            // Initialize from ViewModel (load from persistence) - only on first load
            // After first initialization, preserve _panOffset even if Rebuild is called again
            if (!_hasInitialized)
            {
                _panOffset = ViewModel.PanOffset;
                _zoom = ViewModel.Zoom;
                Log($"    First initialization: PanOffset={_panOffset}, Zoom={_zoom}");
                _hasInitialized = true;
            }
            else
            {
                Log($"    Rebuild called again, preserving PanOffset={_panOffset}, Zoom={_zoom}");
            }

            SyncNodes();

            // Force layout update to ensure NodeControls have been measured/arranged
            InvalidateArrange();

            // Defer SyncWires until after the first layout/render pass so that
            // TranslatePoint returns valid coordinates.  Calling it immediately here
            // (before NodeControls are measured/arranged by the visual tree) leaves
            // every AbsolutePosition at (0,0), making all wires invisible until the
            // user moves a node and triggers a re-sync.
            Avalonia.Threading.Dispatcher.UIThread.Post(
                SyncWires,
                Avalonia.Threading.DispatcherPriority.Render
            );

            // Schedule additional SyncWires calls to ensure wires are rendered even if layout isn't complete
            Avalonia.Threading.Dispatcher.UIThread.Post(
                () =>
                {
                    if (ViewModel is not null)
                    {
                        InvalidateArrange();
                        RequestWireSync();
                    }
                },
                Avalonia.Threading.DispatcherPriority.Background
            );

            SyncTransform();
            Log($"<<< REBUILD completed");
        }
        catch (Exception ex)
        {
            Log($"!!! ERROR in Rebuild: {ex.GetType().Name}: {ex.Message}");
            Log($"    StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    private void SyncNodes()
    {
        try
        {
            if (ViewModel is null)
                return;

            Log($"    SyncNodes: Starting sync for {ViewModel.Nodes.Count} nodes");

            // Add controls for nodes not yet in the cache
            foreach (NodeViewModel vm in ViewModel.Nodes)
            {
                if (_nodeControlCache.ContainsKey(vm))
                    continue;
                var nc = new NodeControl { DataContext = vm };
                Canvas.SetLeft(nc, vm.Position.X);
                Canvas.SetTop(nc, vm.Position.Y);
                nc.ZIndex = vm.ZOrder;
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
                    try
                    {
                        if (e.PropertyName == nameof(NodeViewModel.Position))
                        {
                            Log($"    !!! NODE POSITION CHANGED: {capturedVm.Title}, New Position={capturedVm.Position}");
                            Canvas.SetLeft(capturedNc, capturedVm.Position.X);
                            Canvas.SetTop(capturedNc, capturedVm.Position.Y);
                            Log($"    Canvas position set for {capturedVm.Title}");
                            RequestWireSync();
                            Log($"    RequestWireSync queued for position change");
                            InvalidateArrange();
                            Log($"    InvalidateArrange called for position change");
                        }
                        else if (e.PropertyName == nameof(NodeViewModel.ZOrder))
                        {
                            capturedNc.ZIndex = capturedVm.ZOrder;
                            RequestWireSync();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"!!! ERROR in NodePosition PropertyChanged handler: {ex.GetType().Name}: {ex.Message}");
                        Log($"    StackTrace: {ex.StackTrace}");
                        // Don't rethrow - this could prevent the whole app from continuing
                    }
                };
                vm.PropertyChanged += posHandler;
                _nodePositionHandlers[vm] = posHandler;
                _nodeControlCache[vm] = nc;
                _scene.Children.Add(nc);
                Log($"      Added node '{vm.Title}' at {vm.Position}");
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
                Log($"      Removed node '{vm.Title}'");
            }

            NormalizeNodeZOrder();

            // Ensure wires stay on top after adding/removing nodes
            EnsureWiresOnTop();
            Log($"    SyncNodes: Completed. Cache: {_nodeControlCache.Count} nodes");
        }
        catch (Exception ex)
        {
            Log($"!!! ERROR in SyncNodes: {ex.GetType().Name}: {ex.Message}");
            Log($"    StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    private void EnsureWiresOnTop()
    {
        // Layering model:
        // - wire layer behind nodes
        // - nodes ordered by NodeViewModel.ZOrder
        _wires.ZIndex = -10_000;
        if (_rubberBandRect is not null)
            _rubberBandRect.ZIndex = -9_999;
    }

    private void SyncWires()
    {
        try
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
            Log($"    SyncWires: {ViewModel.Connections.Count} connections synced");
        }
        catch (Exception ex)
        {
            Log($"!!! ERROR in SyncWires: {ex.GetType().Name}: {ex.Message}");
            Log($"    StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    public void InvalidateWires() => SyncWires();

    private void SyncTransform()
    {
        try
        {
            if (ViewModel is null)
                return;
            _zoom = ViewModel.Zoom;
            _panOffset = ViewModel.PanOffset;
            Log($"    SyncTransform: Zoom={_zoom}, PanOffset={_panOffset}");

            // Apply immediately (not only on next layout pass) to avoid transient
            // or missed viewport states during fast wheel interactions.
            _scene.RenderTransform = new TransformGroup
            {
                Children =
                [
                    new ScaleTransform(_zoom, _zoom),
                    new TranslateTransform(_panOffset.X, _panOffset.Y),
                ],
            };

            InvalidateArrange();
            _grid.Zoom = _zoom;
            _grid.PanOffset = _panOffset;
            _grid.InvalidateVisual();
            _wires.InvalidateVisual();
        }
        catch (Exception ex)
        {
            Log($"!!! ERROR in SyncTransform: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    private void UpdatePinPositions()
    {
        try
        {
            if (ViewModel is null)
                return;
            int updatedPins = 0;
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
                            updatedPins++;
                        }
                    }
                }
            }
            Log($"    UpdatePinPositions: Updated {updatedPins} pins");
        }
        catch (Exception ex)
        {Log($"!!! ERROR in UpdatePinPositions: {ex.GetType().Name}: {ex.Message}");
            Log($"    StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    // viewport + layer helpers moved to InfiniteCanvas.ViewportAndLayers.cs
    // pointer + keyboard + context menu + guides moved to InfiniteCanvas.Interaction.cs
    // node drag/click workflow moved to InfiniteCanvas.NodeDrag.cs
}

