using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

public sealed partial class NodeControl : UserControl
{
    public event EventHandler<(NodeViewModel Node, bool ShiftHeld)>? NodeClicked;
    public event EventHandler<(NodeViewModel Node, Point ScreenPos)>? NodeDragStarted;
    public event EventHandler<(NodeViewModel Node, Point ScreenPos)>? NodeDragDelta;
    public event EventHandler<(NodeViewModel Node, Point ScreenPos)>? NodeDragCompleted;
    public event EventHandler<(PinViewModel Pin, Point CanvasPoint)>? PinPressed;

    private bool _dragging; private Point _pressPos; private bool _didDrag;

    public NodeControl()
    {
        InitializeComponent();
        PointerPressed  += OnPressed;
        PointerMoved    += OnMoved;
        PointerReleased += OnReleased;
        PointerEntered  += (_,_) => { if (DataContext is NodeViewModel vm) vm.IsHovered=true; };
        PointerExited   += (_,_) => { if (DataContext is NodeViewModel vm) vm.IsHovered=false; };
    }

    private void OnPressed(object? s, PointerPressedEventArgs e)
    {
        if (DataContext is not NodeViewModel vm) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var pin=HitTestPin(e.GetPosition(this));
        if (pin is not null)
        {
            var cvm=FindCanvasVm();
            var cvmPos=cvm?.ScreenToCanvas(e.GetPosition(Parent as Visual))??e.GetPosition(Parent as Visual);
            PinPressed?.Invoke(this,(pin,cvmPos));
            e.Handled=true; return;
        }
        _dragging=true; _didDrag=false; _pressPos=e.GetPosition(Parent as Visual);
        e.Pointer.Capture(this);
        NodeDragStarted?.Invoke(this,(vm,_pressPos));
        e.Handled=true;
    }

    private void OnMoved(object? s, PointerEventArgs e)
    {
        if (!_dragging||DataContext is not NodeViewModel vm) return;
        _didDrag=true;
        NodeDragDelta?.Invoke(this,(vm,e.GetPosition(Parent as Visual)));
    }

    private void OnReleased(object? s, PointerReleasedEventArgs e)
    {
        if (!_dragging||DataContext is not NodeViewModel vm) return;
        _dragging=false;
        NodeDragCompleted?.Invoke(this,(vm,e.GetPosition(Parent as Visual)));
        e.Pointer.Capture(null);
        if (!_didDrag) NodeClicked?.Invoke(this,(vm,e.KeyModifiers.HasFlag(KeyModifiers.Shift)));
    }

    private PinViewModel? HitTestPin(Point local)
    {
        if (DataContext is not NodeViewModel vm) return null;
        const double tol=10;
        foreach (var b in this.GetLogicalDescendants().OfType<Border>())
        {
            if (b.DataContext is not PinViewModel pvm) continue;
            var c=new Point(b.Bounds.X+b.Bounds.Width/2, b.Bounds.Y+b.Bounds.Height/2);
            var dx=local.X-c.X; var dy=local.Y-c.Y;
            if (Math.Sqrt(dx*dx+dy*dy)<tol) return pvm;
        }
        return null;
    }

    private CanvasViewModel? FindCanvasVm()
    {
        var p=this.GetLogicalParent();
        while (p is not null)
        {
            if (p is Control { DataContext: CanvasViewModel vm }) return vm;
            p=p.GetLogicalParent();
        }
        return null;
    }
}
