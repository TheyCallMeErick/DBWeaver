using Avalonia.Controls;
using Avalonia.Input;
using Avalonia;
using DBWeaver.Metadata;
using DBWeaver.UI.Controls.DragDrop;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Controls;

public partial class SchemaControl : UserControl
{
    private SchemaObjectViewModel? _dragCandidate;
    private Point _dragStartPoint;
    private bool _isDraggingSchemaObject;

    public SchemaControl()
    {
        InitializeComponent();
    }

    private void OnSchemaObjectPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || sender is not Control control)
            return;

        if (control.DataContext is not SchemaObjectViewModel item || item.Data is not TableMetadata)
            return;

        _dragCandidate = item;
        _dragStartPoint = e.GetPosition(this);
        _isDraggingSchemaObject = false;
    }

    private async void OnSchemaObjectPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragCandidate is null || _isDraggingSchemaObject || sender is not Control)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        Point current = e.GetPosition(this);
        if (!HasReachedDragThreshold(_dragStartPoint, current))
            return;

        _isDraggingSchemaObject = true;
        try
        {
            DataObject data = new();
            data.Set(CanvasDragDropDataFormats.SchemaTableFullName, _dragCandidate.SubText);
            await Avalonia.Input.DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
        }
        finally
        {
            _dragCandidate = null;
            _isDraggingSchemaObject = false;
        }
    }

    private void OnSchemaObjectPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _ = sender;
        _ = e;
        _dragCandidate = null;
        _isDraggingSchemaObject = false;
    }

    private static bool HasReachedDragThreshold(Point start, Point current)
    {
        const double threshold = 6.0;
        return Math.Abs(current.X - start.X) >= threshold || Math.Abs(current.Y - start.Y) >= threshold;
    }
}
