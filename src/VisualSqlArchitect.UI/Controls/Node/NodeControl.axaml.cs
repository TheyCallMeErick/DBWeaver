using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

public sealed partial class NodeControl : UserControl
{
    public event EventHandler<(NodeViewModel Node, bool ShiftHeld)>? NodeClicked;
    public event EventHandler<NodeViewModel>? NodeDoubleClicked;
    public event EventHandler<(NodeViewModel Node, Point ScreenPos)>? NodeDragStarted;
    public event EventHandler<(NodeViewModel Node, Point ScreenPos)>? NodeDragDelta;
    public event EventHandler<(NodeViewModel Node, Point ScreenPos)>? NodeDragCompleted;
    public event EventHandler<(PinViewModel Pin, Point CanvasPoint)>? PinPressed;

    private bool _dragging;
    private Point _pressPos;
    private bool _didDrag;
    private bool _isDoubleClick;

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

            HookWindowSlotButton("AddPartitionBtn", vm => vm.AddWindowPartitionSlot());
            HookWindowSlotButton("RemovePartitionBtn", vm => vm.RemoveWindowPartitionSlot());
            HookWindowSlotButton("AddOrderBtn", vm => vm.AddWindowOrderSlot());
            HookWindowSlotButton("RemoveOrderBtn", vm => vm.RemoveWindowOrderSlot());

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

    private void HookWindowSlotButton(string buttonName, Action<NodeViewModel> action)
    {
        Button? button = this.FindControl<Button>(buttonName);
        if (button is null)
            return;

        button.Click += (_, e) =>
        {
            e.Handled = true;
            if (DataContext is NodeViewModel vm)
                action(vm);
        };
    }

    private void OnPressed(object? s, PointerPressedEventArgs e)
    {
        if (DataContext is not NodeViewModel vm)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // Let embedded editors (ComboBox/TextBox/Button/CheckBox) handle interaction.
        // Without this guard, node drag capture can block in-node selectors like JOIN type.
        if (IsInteractiveTarget(e.Source))
            return;

        _isDoubleClick = e.ClickCount >= 2;

        // Let canvas handle space+drag panning (do not start node/pin drag here).
        InfiniteCanvas? canvas = this.FindLogicalAncestorOfType<InfiniteCanvas>();
        if (canvas is not null && canvas.IsSpacePanModeArmed)
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
        {
            NodeClicked?.Invoke(this, (vm, e.KeyModifiers.HasFlag(KeyModifiers.Shift)));
            if (_isDoubleClick)
                NodeDoubleClicked?.Invoke(this, vm);
        }

        _isDoubleClick = false;
    }

    private static bool IsInteractiveTarget(object? source)
    {
        if (source is not Visual visual)
            return false;

        return visual.FindAncestorOfType<ComboBox>() is not null
            || visual.FindAncestorOfType<TextBox>() is not null
            || visual.FindAncestorOfType<Button>() is not null
            || visual.FindAncestorOfType<CheckBox>() is not null;
    }

    private PinViewModel? HitTestPin(Point local)
    {
        if (DataContext is not NodeViewModel)
            return null;
        const double tol = 10;

        foreach (PinShapeControl psc in this.GetLogicalDescendants().OfType<PinShapeControl>())
        {
            if (psc.DataContext is not PinViewModel pvm)
                continue;

            Point? translated = psc.TranslatePoint(
                new Point(psc.Bounds.Width / 2, psc.Bounds.Height / 2),
                this
            );

            if (translated is null)
                continue;

            double dx = local.X - translated.Value.X;
            double dy = local.Y - translated.Value.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < tol)
                return pvm;
        }

        // Backward-compatible fallback for legacy templates still using Border dots.
        foreach (Border b in this.GetLogicalDescendants().OfType<Border>())
        {
            if (b.DataContext is not PinViewModel pvm)
                continue;

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
