using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

public sealed class InfiniteCanvas : Panel
{
    public static readonly StyledProperty<CanvasViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<InfiniteCanvas, CanvasViewModel?>(nameof(ViewModel));

    public CanvasViewModel? ViewModel
    { get => GetValue(ViewModelProperty); set => SetValue(ViewModelProperty, value); }

    static InfiniteCanvas()
    {
        ViewModelProperty.Changed.AddClassHandler<InfiniteCanvas>((c,_) => c.Rebuild());
        ClipToBoundsProperty.OverrideDefaultValue<InfiniteCanvas>(true);
    }

    private readonly DotGridBackground _grid  = new() { IsHitTestVisible = false };
    private readonly Canvas            _scene = new();
    private readonly BezierWireLayer   _wires = new();

    private PinDragInteraction? _pinDrag;
    private double _zoom = 1.0;
    private Point  _panOffset;
    private bool   _isPanning;
    private Point  _panStart;
    private bool   _isRubberBanding;
    private Point  _rubberStart, _rubberCurrent;
    private NodeViewModel? _dragNode;
    private Point _nodeDragStart, _nodePosStart;

    public InfiniteCanvas()
    {
        Children.Add(_grid);
        Children.Add(_scene);
        _scene.Children.Add(_wires);
        PointerWheelChanged += OnWheel;
        PointerPressed      += OnPressed;
        PointerMoved        += OnMoved;
        PointerReleased     += OnReleased;
        KeyDown             += OnKey;
        Focusable            = true;
    }

    protected override Size MeasureOverride(Size s)
    { _grid.Measure(s); _scene.Measure(Size.Infinity); return s; }

    protected override Size ArrangeOverride(Size s)
    {
        _grid.Arrange(new Rect(s)); _grid.Width=s.Width; _grid.Height=s.Height;
        _grid.Zoom=_zoom; _grid.PanOffset=_panOffset;

        _scene.RenderTransform = new TransformGroup { Children = [
            new ScaleTransform(_zoom,_zoom), new TranslateTransform(_panOffset.X,_panOffset.Y) ] };
        _scene.Arrange(new Rect(new Point(-_panOffset.X/_zoom,-_panOffset.Y/_zoom), new Size(20000,20000)));

        foreach (var nc in _scene.Children.OfType<NodeControl>())
            if (nc.DataContext is NodeViewModel vm)
            {
                Canvas.SetLeft(nc,vm.Position.X); Canvas.SetTop(nc,vm.Position.Y);
                nc.Measure(Size.Infinity); nc.Arrange(new Rect(nc.DesiredSize));
            }

        _wires.Arrange(new Rect(new Size(20000,20000)));
        return s;
    }

    private void Rebuild()
    {
        foreach (var nc in _scene.Children.OfType<NodeControl>().ToList()) _scene.Children.Remove(nc);
        if (ViewModel is null) return;
        _pinDrag = new PinDragInteraction(ViewModel, _scene);
        ViewModel.Nodes.CollectionChanged       += (_,_) => SyncNodes();
        ViewModel.Connections.CollectionChanged += (_,_) => SyncWires();
        ViewModel.PropertyChanged += (_,e) => { if (e.PropertyName is nameof(CanvasViewModel.Zoom) or nameof(CanvasViewModel.PanOffset)) SyncTransform(); };
        SyncNodes(); SyncWires(); SyncTransform();
    }

    private void SyncNodes()
    {
        if (ViewModel is null) return;
        var existing = _scene.Children.OfType<NodeControl>().ToDictionary(nc=>nc.DataContext as NodeViewModel);
        foreach (var vm in ViewModel.Nodes)
        {
            if (existing.ContainsKey(vm)) continue;
            var nc = new NodeControl { DataContext = vm };
            Canvas.SetLeft(nc,vm.Position.X); Canvas.SetTop(nc,vm.Position.Y);
            nc.NodeDragStarted   += OnNodeDragStarted;
            nc.NodeDragDelta     += OnNodeDragDelta;
            nc.NodeDragCompleted += OnNodeDragCompleted;
            nc.NodeClicked       += OnNodeClicked;
            nc.PinPressed        += OnPinPressed;
            vm.PropertyChanged   += (_,e) => { if (e.PropertyName==nameof(NodeViewModel.Position)) { Canvas.SetLeft(nc,vm.Position.X); Canvas.SetTop(nc,vm.Position.Y); SyncWires(); InvalidateArrange(); }};
            _scene.Children.Add(nc);
        }
        foreach (var (vm,nc) in existing)
            if (vm is null || !ViewModel.Nodes.Contains(vm))
            {
                nc.NodeDragStarted-=OnNodeDragStarted; nc.NodeDragDelta-=OnNodeDragDelta;
                nc.NodeDragCompleted-=OnNodeDragCompleted; nc.NodeClicked-=OnNodeClicked; nc.PinPressed-=OnPinPressed;
                _scene.Children.Remove(nc);
            }
    }

    private void SyncWires()
    {
        if (ViewModel is null) return;
        UpdatePinPositions();
        foreach (var c in ViewModel.Connections)
        {
            c.FromPoint = c.FromPin.AbsolutePosition;
            if (c.ToPin is not null) c.ToPoint = c.ToPin.AbsolutePosition;
        }
        _wires.Connections = ViewModel.Connections.ToList();
        _wires.PendingConnection = _pinDrag?.LiveWire;
        _wires.InvalidateVisual();
    }

    public void InvalidateWires() => SyncWires();

    private void SyncTransform()
    {
        if (ViewModel is null) return;
        _zoom=ViewModel.Zoom; _panOffset=ViewModel.PanOffset;
        InvalidateArrange(); _grid.Zoom=_zoom; _grid.PanOffset=_panOffset; _grid.InvalidateVisual();
    }

    private void UpdatePinPositions()
    {
        if (ViewModel is null) return;
        const double headerH=46, pinH=26, padTop=4;
        foreach (var node in ViewModel.Nodes)
        {
            var nc = _scene.Children.OfType<NodeControl>().FirstOrDefault(c=>c.DataContext==node);
            var nodeW = nc?.Bounds.Width>0 ? nc.Bounds.Width : 220;
            for (int i=0;i<node.InputPins.Count;i++)
                node.InputPins[i].AbsolutePosition = new Point(node.Position.X, node.Position.Y+headerH+padTop+i*(pinH+2)+pinH/2.0);
            for (int i=0;i<node.OutputPins.Count;i++)
                node.OutputPins[i].AbsolutePosition = new Point(node.Position.X+nodeW, node.Position.Y+headerH+padTop+i*(pinH+2)+pinH/2.0);
        }
    }

    private void OnNodeClicked(object? s, (NodeViewModel Node, bool Shift) a) => ViewModel?.SelectNode(a.Node, a.Shift);

    private void OnNodeDragStarted(object? s, (NodeViewModel Node, Point Pos) a)
    { _dragNode=a.Node; _nodeDragStart=a.Pos; _nodePosStart=a.Node.Position; }

    private void OnNodeDragDelta(object? s, (NodeViewModel Node, Point Pos) a)
    {
        if (_dragNode is null) return;
        var d=a.Pos-_nodeDragStart;
        _dragNode.Position=new Point(_nodePosStart.X+d.X/_zoom, _nodePosStart.Y+d.Y/_zoom);
        SyncWires();
    }

    private void OnNodeDragCompleted(object? s, (NodeViewModel Node, Point Pos) a)
    {
        if (_dragNode is not null && ViewModel is not null && _dragNode.Position!=_nodePosStart)
        {
            var final=_dragNode.Position;
            _dragNode.Position=_nodePosStart;
            ViewModel.UndoRedo.Execute(new MoveNodeCommand(_dragNode,_nodePosStart,final));
        }
        _dragNode=null;
    }

    private void OnPinPressed(object? s, (PinViewModel Pin, Point Canvas) a)
    {
        _pinDrag?.BeginDrag(a.Pin, a.Canvas);
        _wires.PendingConnection=_pinDrag?.LiveWire;
    }

    private void OnWheel(object? s, PointerWheelEventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.ZoomToward(e.GetPosition(this), e.Delta.Y>0?1.10:0.91);
        SyncTransform(); e.Handled=true;
    }

    private void OnPressed(object? s, PointerPressedEventArgs e)
    {
        Focus();
        var props=e.GetCurrentPoint(this).Properties;
        var screen=e.GetPosition(this);

        if (props.IsMiddleButtonPressed||(props.IsLeftButtonPressed&&e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        { _isPanning=true; _panStart=screen-(Vector)_panOffset; e.Pointer.Capture(this); Cursor=new Cursor(StandardCursorType.SizeAll); e.Handled=true; return; }

        if (props.IsLeftButtonPressed)
        {
            var canvas=ScreenToCanvas(screen);
            var pin=_pinDrag?.HitTestPin(canvas);
            if (pin is not null) { _pinDrag!.BeginDrag(pin,canvas); _wires.PendingConnection=_pinDrag.LiveWire; e.Pointer.Capture(this); e.Handled=true; return; }
            ViewModel?.DeselectAll();
            _isRubberBanding=true; _rubberStart=canvas; _rubberCurrent=canvas;
        }

        if (props.IsRightButtonPressed) ShowContextMenu(screen);
    }

    private void OnMoved(object? s, PointerEventArgs e)
    {
        var screen=e.GetPosition(this); var canvas=ScreenToCanvas(screen);
        if (_isPanning&&ViewModel is not null) { ViewModel.PanOffset=screen-(Vector)_panStart; SyncTransform(); return; }
        if (_pinDrag?.IsDragging==true) { _pinDrag.UpdateDrag(canvas); _wires.PendingConnection=_pinDrag.LiveWire; _wires.InvalidateVisual(); return; }
        if (_isRubberBanding) { _rubberCurrent=canvas; UpdateRubberBand(); UpdateRubberBandVisual(); }
    }

    private void OnReleased(object? s, PointerReleasedEventArgs e)
    {
        var canvas=ScreenToCanvas(e.GetPosition(this));
        if (_isPanning) { _isPanning=false; e.Pointer.Capture(null); Cursor=Cursor.Default; return; }
        if (_pinDrag?.IsDragging==true) { _pinDrag.EndDrag(canvas); _wires.PendingConnection=null; _wires.InvalidateVisual(); e.Pointer.Capture(null); return; }
        if (_isRubberBanding) { _isRubberBanding=false; UpdateRubberBand(); UpdateRubberBandVisual(); }
    }

    private void OnKey(object? s, KeyEventArgs e)
    {
        if (ViewModel is null) return;
        if (e.Key==Key.A&&e.KeyModifiers.HasFlag(KeyModifiers.Shift)) { ViewModel.SearchMenu.Open(ScreenToCanvas(new Point(Bounds.Width/2,Bounds.Height/2))); e.Handled=true; return; }
        if (e.Key==Key.F3) { ViewModel.DataPreview.Toggle(); e.Handled=true; return; }
        if (e.Key==Key.Z&&e.KeyModifiers.HasFlag(KeyModifiers.Control)) { ViewModel.UndoRedo.Undo(); e.Handled=true; return; }
        if (e.Key==Key.Y&&e.KeyModifiers.HasFlag(KeyModifiers.Control)) { ViewModel.UndoRedo.Redo(); e.Handled=true; return; }
        if (e.Key is Key.Delete or Key.Back) { ViewModel.DeleteSelected(); e.Handled=true; return; }
        if (e.Key==Key.A&&e.KeyModifiers.HasFlag(KeyModifiers.Control)) { ViewModel.SelectAll(); e.Handled=true; return; }
        if (e.Key==Key.Escape) { _pinDrag?.CancelDrag(); _wires.PendingConnection=null; _wires.InvalidateVisual(); ViewModel.DeselectAll(); ViewModel.SearchMenu.Close(); _isRubberBanding=false; UpdateRubberBandVisual(); e.Handled=true; }
    }

    private void UpdateRubberBand()
    {
        if (ViewModel is null) return;
        var r=new Rect(Math.Min(_rubberStart.X,_rubberCurrent.X),Math.Min(_rubberStart.Y,_rubberCurrent.Y),Math.Abs(_rubberCurrent.X-_rubberStart.X),Math.Abs(_rubberCurrent.Y-_rubberStart.Y));
        foreach (var n in ViewModel.Nodes) n.IsSelected=r.Contains(n.Position);
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

        var x = _panOffset.X + Math.Min(_rubberStart.X, _rubberCurrent.X) * _zoom;
        var y = _panOffset.Y + Math.Min(_rubberStart.Y, _rubberCurrent.Y) * _zoom;
        var w = Math.Abs(_rubberCurrent.X - _rubberStart.X) * _zoom;
        var h = Math.Abs(_rubberCurrent.Y - _rubberStart.Y) * _zoom;

        if (_rubberBandRect == null)
        {
            _rubberBandRect = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#103B82F6")),
                BorderBrush = new SolidColorBrush(Color.Parse("#3B82F6")),
                BorderThickness = new Thickness(1)
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
        if (ViewModel is null) return;
        var sel=ViewModel.Nodes.Where(n=>n.IsSelected).ToList();
        var menu=new ContextMenu();
        if (sel.Count>0) menu.Items.Add(new MenuItem { Header=$"Delete {(sel.Count==1?sel[0].Title:$"{sel.Count} nodes")}", Command=new RelayCommand(ViewModel.DeleteSelected) });
        else menu.Items.Add(new MenuItem { Header="Add Node (Shift+A)", Command=new RelayCommand(()=>ViewModel.SearchMenu.Open(ScreenToCanvas(screen))) });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header=$"Undo {ViewModel.UndoRedo.UndoDescription}", Command=ViewModel.UndoCommand });
        menu.Items.Add(new MenuItem { Header="Redo", Command=ViewModel.RedoCommand });
        menu.Open(this);
    }

    private Point ScreenToCanvas(Point s) => new((s.X-_panOffset.X)/_zoom,(s.Y-_panOffset.Y)/_zoom);
}
