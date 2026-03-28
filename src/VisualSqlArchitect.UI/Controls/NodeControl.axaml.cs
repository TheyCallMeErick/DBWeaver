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

    private bool _dragging;
    private Point _pressPos;
    private bool _didDrag;

    public NodeControl()
    {
        InitializeComponent();

        Button? previewToggle = this.FindControl<Button>("PreviewToggleBtn");
        if (previewToggle is not null)
            previewToggle.Click += async (_, e) =>
            {
                // Don't let click bubble into node drag
                e.Handled = true;
                if (DataContext is NodeViewModel vm)
                    await vm.ToggleInlinePreviewAsync();
            };

        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
        PointerEntered += (_, _) =>
        {
            if (DataContext is NodeViewModel vm)
                vm.IsHovered = true;
        };
        PointerExited += (_, _) =>
        {
            if (DataContext is NodeViewModel vm)
                vm.IsHovered = false;
        };
    }

    private void OnPressed(object? s, PointerPressedEventArgs e)
    {
        if (DataContext is not NodeViewModel vm)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        PinViewModel? pin = HitTestPin(e.GetPosition(this));
        if (pin is not null)
        {
            CanvasViewModel? cvm = FindCanvasVm();
            Point cvmPos =
                cvm?.ScreenToCanvas(e.GetPosition(Parent as Visual))
                ?? e.GetPosition(Parent as Visual);
            PinPressed?.Invoke(this, (pin, cvmPos));
            // Do NOT set e.Handled=true here — let the event bubble to InfiniteCanvas
            // so it can capture the pointer and own the pin-drag move/release events.
            return;
        }
        _dragging = true;
        _didDrag = false;
        _pressPos = e.GetPosition(Parent as Visual);
        e.Pointer.Capture(this);
        NodeDragStarted?.Invoke(this, (vm, _pressPos));
        e.Handled = true;
    }

    private void OnMoved(object? s, PointerEventArgs e)
    {
        if (!_dragging || DataContext is not NodeViewModel vm)
            return;
        _didDrag = true;
        NodeDragDelta?.Invoke(this, (vm, e.GetPosition(Parent as Visual)));
    }

    private void OnReleased(object? s, PointerReleasedEventArgs e)
    {
        if (!_dragging || DataContext is not NodeViewModel vm)
            return;
        _dragging = false;
        NodeDragCompleted?.Invoke(this, (vm, e.GetPosition(Parent as Visual)));
        e.Pointer.Capture(null);
        if (!_didDrag)
            NodeClicked?.Invoke(this, (vm, e.KeyModifiers.HasFlag(KeyModifiers.Shift)));
    }

    private PinViewModel? HitTestPin(Point local)
    {
        if (DataContext is not NodeViewModel)
            return null;
        const double tol = 10;
        foreach (Border b in this.GetLogicalDescendants().OfType<Border>())
        {
            if (b.DataContext is not PinViewModel pvm)
                continue;
            // TranslatePoint converts from the Border's own coordinate space to the
            // NodeControl's coordinate space, correctly handling nested DataTemplate
            // Grids and negative margins (e.g. the -5px on pin dots).
            Point? translated = b.TranslatePoint(
                new Point(b.Bounds.Width / 2, b.Bounds.Height / 2),
                this
            );
            if (translated is null)
                continue;
            double dx = local.X - translated.Value.X;
            double dy = local.Y - translated.Value.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < tol)
                return pvm;
        }
        return null;
    }

    private CanvasViewModel? FindCanvasVm()
    {
        ILogical? p = this.GetLogicalParent();
        while (p is not null)
        {
            if (p is Control { DataContext: CanvasViewModel vm })
                return vm;
            p = p.GetLogicalParent();
        }
        return null;
    }
}
