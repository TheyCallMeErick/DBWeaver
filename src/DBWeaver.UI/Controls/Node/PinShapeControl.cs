using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DBWeaver.Nodes;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.Controls;

/// <summary>
/// Renders pin shapes with family-specific geometry.
/// Shape encodes semantic family; stroke/fill encode pin state.
/// </summary>
public sealed class PinShapeControl : Control
{
    private const double DefaultPinSize = 10;

    private enum EPinGeometryKind
    {
        Circle,
        ColumnRefDiamond,
        ColumnSetDiamond,
        RowSetDiamond,
        TableDefRoundedSquare,
        ColumnDefDoubleCircle,
        ConstraintDiamond,
        TypeDefDiamond,
        IndexDefTriangle,
        AlterOpRoundedArrow,
    }

    public static readonly StyledProperty<PinDataType> DataTypeProperty =
        AvaloniaProperty.Register<PinShapeControl, PinDataType>(nameof(DataType));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<PinShapeControl, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<PinShapeControl, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<bool> IsDropTargetProperty =
        AvaloniaProperty.Register<PinShapeControl, bool>(nameof(IsDropTarget));

    public static readonly StyledProperty<double> ScaleProperty =
        AvaloniaProperty.Register<PinShapeControl, double>(nameof(Scale), 1.0);

    public static readonly StyledProperty<double> YOffsetProperty =
        AvaloniaProperty.Register<PinShapeControl, double>(nameof(YOffset), 0.0);

    static PinShapeControl()
    {
        AffectsRender<PinShapeControl>(
            DataTypeProperty,
            StrokeProperty,
            FillProperty,
            IsDropTargetProperty,
            ScaleProperty,
            YOffsetProperty
        );
    }

    public PinDataType DataType
    {
        get => GetValue(DataTypeProperty);
        set => SetValue(DataTypeProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public bool IsDropTarget
    {
        get => GetValue(IsDropTargetProperty);
        set => SetValue(IsDropTargetProperty, value);
    }

    public double Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    /// <summary>
    /// Visual-only vertical offset in pixels. Does not change control layout bounds,
    /// useful to fine-tune glyph alignment against text baselines while keeping wire anchors stable.
    /// </summary>
    public double YOffset
    {
        get => GetValue(YOffsetProperty);
        set => SetValue(YOffsetProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Ensure pins always reserve a visible footprint even when parent layouts
        // provide unconstrained/auto sizing during template composition.
        double width = double.IsNaN(Width) ? DefaultPinSize : Width;
        double height = double.IsNaN(Height) ? DefaultPinSize : Height;

        if (!double.IsInfinity(availableSize.Width))
            width = Math.Min(width, availableSize.Width);
        if (!double.IsInfinity(availableSize.Height))
            height = Math.Min(height, availableSize.Height);

        return new Size(Math.Max(0, width), Math.Max(0, height));
    }

    public override void Render(DrawingContext context)
    {
        Rect bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        IBrush? stroke = IsDropTarget ? new SolidColorBrush(Color.Parse(UiColorConstants.C_FBBF24)) : Stroke;
        var pen = new Pen(stroke, DataType == PinDataType.ColumnSet ? 2.2 : 1.9);
        if (DataType == PinDataType.Expression)
            pen.DashStyle = new DashStyle([2, 2], 0);

        Point center = new(bounds.Center.X, bounds.Center.Y + YOffset);
        double sx = Math.Max(0.1, Scale);
        double halfW = (bounds.Width * 0.5) * sx;
        double halfH = (bounds.Height * 0.5) * sx;

        switch (ResolveGeometryKind(DataType))
        {
            case EPinGeometryKind.ColumnRefDiamond:
                DrawDiamond(context, center, halfW, halfH, Fill, pen);
                break;

            case EPinGeometryKind.ColumnSetDiamond:
                DrawDiamond(context, center, halfW, halfH, Fill, pen);
                DrawDiamond(context, center, halfW * 0.38, halfH * 0.38, Brushes.Transparent, new Pen(stroke, 1));
                break;

            case EPinGeometryKind.RowSetDiamond:
                DrawDiamond(context, center, halfW, halfH * 0.7, Fill, pen);
                break;

            case EPinGeometryKind.TableDefRoundedSquare:
                DrawRoundedSquare(context, center, halfW * 0.95, halfH * 0.95, Fill, pen);
                break;

            case EPinGeometryKind.ColumnDefDoubleCircle:
                context.DrawEllipse(Fill, pen, center, halfW, halfH);
                context.DrawEllipse(Brushes.Transparent, new Pen(stroke, 1), center, halfW * 0.55, halfH * 0.55);
                break;

            case EPinGeometryKind.ConstraintDiamond:
                DrawDiamond(context, center, halfW, halfH, Fill, pen);
                break;

            case EPinGeometryKind.TypeDefDiamond:
                DrawDiamond(context, center, halfW, halfH, Fill, pen);
                DrawDiamond(context, center, halfW * 0.42, halfH * 0.42, Brushes.Transparent, new Pen(stroke, 1));
                break;

            case EPinGeometryKind.IndexDefTriangle:
                DrawTriangle(context, center, halfW, halfH, Fill, pen);
                break;

            case EPinGeometryKind.AlterOpRoundedArrow:
                DrawRoundedArrow(context, center, halfW, halfH, Fill, pen);
                break;

            default:
                context.DrawEllipse(Fill, pen, center, halfW, halfH);
                break;
        }
    }

    private static EPinGeometryKind ResolveGeometryKind(PinDataType dataType) =>
        dataType switch
        {
            PinDataType.ColumnRef => EPinGeometryKind.ColumnRefDiamond,
            PinDataType.ColumnSet => EPinGeometryKind.ColumnSetDiamond,
            PinDataType.RowSet => EPinGeometryKind.RowSetDiamond,
            PinDataType.TableDef => EPinGeometryKind.TableDefRoundedSquare,
            PinDataType.ViewDef => EPinGeometryKind.TableDefRoundedSquare,
            PinDataType.ColumnDef => EPinGeometryKind.ColumnDefDoubleCircle,
            PinDataType.Constraint => EPinGeometryKind.ConstraintDiamond,
            PinDataType.TypeDef => EPinGeometryKind.TypeDefDiamond,
            PinDataType.IndexDef => EPinGeometryKind.IndexDefTriangle,
            PinDataType.AlterOp => EPinGeometryKind.AlterOpRoundedArrow,
            _ => EPinGeometryKind.Circle,
        };

    private static void DrawDiamond(DrawingContext context, Point center, double halfW, double halfH, IBrush? fill, Pen pen)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext gc = geometry.Open())
        {
            gc.BeginFigure(new Point(center.X, center.Y - halfH), true);
            gc.LineTo(new Point(center.X + halfW, center.Y));
            gc.LineTo(new Point(center.X, center.Y + halfH));
            gc.LineTo(new Point(center.X - halfW, center.Y));
            gc.EndFigure(true);
        }

        context.DrawGeometry(fill, pen, geometry);
    }

    private static void DrawTriangle(DrawingContext context, Point center, double halfW, double halfH, IBrush? fill, Pen pen)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext gc = geometry.Open())
        {
            gc.BeginFigure(new Point(center.X, center.Y - halfH), true);
            gc.LineTo(new Point(center.X + halfW, center.Y + halfH));
            gc.LineTo(new Point(center.X - halfW, center.Y + halfH));
            gc.EndFigure(true);
        }

        context.DrawGeometry(fill, pen, geometry);
    }

    private static void DrawRoundedSquare(DrawingContext context, Point center, double halfW, double halfH, IBrush? fill, Pen pen)
    {
        var rect = new Rect(center.X - halfW, center.Y - halfH, halfW * 2, halfH * 2);
        context.DrawRectangle(fill, pen, rect, 2.5, 2.5);
    }

    private static void DrawRoundedArrow(DrawingContext context, Point center, double halfW, double halfH, IBrush? fill, Pen pen)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext gc = geometry.Open())
        {
            double left = center.X - halfW;
            double right = center.X + halfW;
            double top = center.Y - halfH * 0.7;
            double bottom = center.Y + halfH * 0.7;
            double shaftEnd = center.X + halfW * 0.18;

            gc.BeginFigure(new Point(left, top), true);
            gc.LineTo(new Point(shaftEnd, top));
            gc.LineTo(new Point(right, center.Y));
            gc.LineTo(new Point(shaftEnd, bottom));
            gc.LineTo(new Point(left, bottom));
            gc.EndFigure(true);
        }

        context.DrawGeometry(fill, pen, geometry);
    }
}
