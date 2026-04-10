using Avalonia.Controls;
using Avalonia.Input;
using Avalonia;
using DBWeaver.UI.Controls.DragDrop;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Controls;

public partial class NodesListControl : UserControl
{
    private NodeTypeItemViewModel? _dragCandidate;
    private Point _dragStartPoint;
    private bool _isDraggingNodeCard;

    public NodesListControl()
    {
        InitializeComponent();
    }

    private void OnNodeCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || sender is not Control control)
            return;

        _dragCandidate = control.DataContext as NodeTypeItemViewModel;
        _dragStartPoint = e.GetPosition(this);
        _isDraggingNodeCard = false;
    }

    private async void OnNodeCardPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragCandidate is null || _isDraggingNodeCard || sender is not Control)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        Point current = e.GetPosition(this);
        if (!HasReachedDragThreshold(_dragStartPoint, current))
            return;

        _isDraggingNodeCard = true;
        try
        {
            DataObject data = new();
            data.Set(CanvasDragDropDataFormats.NodeType, _dragCandidate.Definition.Type.ToString());
            await Avalonia.Input.DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
        }
        finally
        {
            _dragCandidate = null;
            _isDraggingNodeCard = false;
        }
    }

    private void OnNodeCardPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _ = sender;
        _ = e;
        _dragCandidate = null;
        _isDraggingNodeCard = false;
    }

    private static bool HasReachedDragThreshold(Point start, Point current)
    {
        const double threshold = 6.0;
        return Math.Abs(current.X - start.X) >= threshold || Math.Abs(current.Y - start.Y) >= threshold;
    }
}
