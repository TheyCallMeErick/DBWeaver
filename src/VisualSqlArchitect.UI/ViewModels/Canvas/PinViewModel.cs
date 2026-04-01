using Avalonia;
using Avalonia.Media;
using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// Represents a Pin (Input or Output) on a Node in the visual query builder.
/// Pins are connection points where data flows between nodes.
/// </summary>
public sealed class PinViewModel(PinDescriptor d, NodeViewModel owner) : ViewModelBase
{
    private Point _absolutePosition;
    private bool _hasAbsolutePosition;
    private bool _isHovered,
        _isConnected,
        _isDropTarget,
        _isDragIncompatible;
    private PinDataType? _narrowedDataType;
    private PinDataType? _expectedColumnScalarType;

    /// <summary>
    /// The unique name of this pin within its parent node.
    /// Examples: "input", "output", "columns", "cond_1"
    /// </summary>
    public string Name { get; } = d.Name;

    /// <summary>
    /// Direction of this pin: Input or Output.
    /// </summary>
    public PinDirection Direction { get; } = d.Direction;

    /// <summary>
    /// The declared data type of this pin.
    /// Can be concrete (Text, Number) or "Any" (allows type narrowing).
    /// </summary>
    public PinDataType DataType { get; } = d.DataType;

    /// <summary>
    /// True if this pin must have at least one connection.
    /// </summary>
    public bool IsRequired { get; } = d.IsRequired;

    /// <summary>
    /// True if this pin can accept multiple connections simultaneously.
    /// Used for ColumnList and AND/OR gates.
    /// </summary>
    public bool AllowMultiple { get; } = d.AllowMultiple;

    public ColumnRefMeta? ColumnRefMeta { get; } = d.ColumnRefMeta;

    public ColumnSetMeta? ColumnSetMeta { get; } = d.ColumnSetMeta;

    /// <summary>
    /// Reference to the parent node that owns this pin.
    /// </summary>
    public NodeViewModel Owner { get; } = owner;

    /// <summary>
    /// When this pin's DataType is "Any", it can be narrowed to a specific type
    /// when connected to a pin with a concrete type. This narrows the acceptable types
    /// for this pin and its sibling pins within the same node.
    /// </summary>
    public PinDataType? NarrowedDataType
    {
        get => _narrowedDataType;
        set
        {
            Set(ref _narrowedDataType, value);
            RaisePropertyChanged(nameof(EffectiveDataType));
            RaisePropertyChanged(nameof(PinColor));
            RaisePropertyChanged(nameof(PinBrush));
            RaisePropertyChanged(nameof(PinGlowBrush));
            RaisePropertyChanged(nameof(PinFillBrush));
            RaisePropertyChanged(nameof(DataTypeLabel));
            RaisePropertyChanged(nameof(ShowOutputFallbackVisual));
        }
    }

    /// <summary>
    /// Returns the effective data type: narrowed type if set, otherwise the original DataType.
    /// </summary>
    public PinDataType EffectiveDataType => NarrowedDataType ?? DataType;

    /// <summary>
    /// Keeps fallback ring limited to scalar-like outputs.
    /// Structural families already have distinctive geometry and should not get a background ring.
    /// </summary>
    public bool ShowOutputFallbackVisual =>
        EffectiveDataType
            is PinDataType.Text
                or PinDataType.Integer
                or PinDataType.Decimal
                or PinDataType.Number
                or PinDataType.Boolean
                or PinDataType.DateTime
                or PinDataType.Json;

    /// <summary>
    /// Color representation of this pin's data type for UI visualization.
    /// </summary>
    public Color PinColor =>
        Owner.Type == NodeType.ResultOutput && Name == "having"
            ? Color.Parse("#FBBF24")
            : EffectiveDataType switch
            {
                PinDataType.Text => Color.Parse("#60A5FA"),
                PinDataType.Integer => Color.Parse("#34D399"),
                PinDataType.Decimal => Color.Parse("#86EFAC"),
                PinDataType.Number => Color.Parse("#4ADE80"),
                PinDataType.Boolean => Color.Parse("#FCD34D"),
                PinDataType.DateTime => Color.Parse("#38BDF8"),
                PinDataType.Json => Color.Parse("#818CF8"),
                PinDataType.ColumnRef => Color.Parse("#FB923C"),
                PinDataType.ColumnSet => Color.Parse("#FBBF24"),
                PinDataType.RowSet => Color.Parse("#F472B6"),
                PinDataType.Expression => Color.Parse("#6B7280"),
                _ => Color.Parse("#94A3B8"),
            };

    /// <summary>
    /// Solid color brush for rendering this pin.
    /// </summary>
    public SolidColorBrush PinBrush => new(PinColor);

    /// <summary>
    /// Semi-transparent glow brush for hover effects.
    /// </summary>
    public SolidColorBrush PinGlowBrush =>
        new(Color.FromArgb(60, PinColor.R, PinColor.G, PinColor.B));

    public SolidColorBrush PinFillBrush =>
        IsConnected
            ? new SolidColorBrush(PinColor)
            : new SolidColorBrush(Color.FromArgb(72, PinColor.R, PinColor.G, PinColor.B));

    /// <summary>
    /// Display label for the data type (e.g., "TEXT", "NUMBER").
    /// </summary>
    public string DataTypeLabel =>
        EffectiveDataType == PinDataType.ColumnRef
            ? GetScalarTypeLabel(ResolveColumnScalarType(this) ?? PinDataType.Number) + "↑"
            : GetScalarTypeLabel(EffectiveDataType);

    public PinDataType? ExpectedColumnScalarType
    {
        get => _expectedColumnScalarType;
        set
        {
            Set(ref _expectedColumnScalarType, value);
            RaisePropertyChanged(nameof(DataTypeLabel));
            RaisePropertyChanged(nameof(TooltipTypeDetails));
        }
    }

    public string TooltipTypeDetails
    {
        get
        {
            if (ColumnRefMeta is not null)
            {
                string qualifier = string.IsNullOrWhiteSpace(ColumnRefMeta.TableAlias)
                    ? ColumnRefMeta.ColumnName
                    : $"{ColumnRefMeta.TableAlias}.{ColumnRefMeta.ColumnName}";

                string nullableText = ColumnRefMeta.IsNullable ? "NULL" : "NOT NULL";
                return $"{qualifier} : {ColumnRefMeta.ScalarType} {nullableText}";
            }

            if (EffectiveDataType == PinDataType.ColumnRef && ExpectedColumnScalarType is not null)
                return $"{Name} : {ExpectedColumnScalarType} (concretized)";

            if (ColumnSetMeta is { Columns.Count: > 0 })
            {
                string firstColumns = string.Join(", ", ColumnSetMeta.Columns.Take(4).Select(c => $"{c.ColumnName}:{GetScalarTypeLabel(c.ScalarType)}"));
                if (ColumnSetMeta.Columns.Count > 4)
                    firstColumns += ", ...";

                return $"ColumnSet[{ColumnSetMeta.Columns.Count}] {firstColumns}";
            }

            if (EffectiveDataType == PinDataType.RowSet)
            {
                string rowSetPreview = BuildRowSetPreview(Owner);
                if (!string.IsNullOrWhiteSpace(rowSetPreview))
                    return rowSetPreview;

                return "RowSet[unknown schema]";
            }

            return EffectiveDataType.ToString();
        }
    }

    /// <summary>
    /// Absolute screen position of this pin for wire drawing.
    /// Updated by InfiniteCanvas.UpdatePinPositions().
    /// </summary>
    public Point AbsolutePosition
    {
        get => _absolutePosition;
        set
        {
            if (Set(ref _absolutePosition, value) && !_hasAbsolutePosition)
            {
                _hasAbsolutePosition = true;
                RaisePropertyChanged(nameof(HasAbsolutePosition));
            }
        }
    }

    /// <summary>
    /// True once this pin has received at least one canvas-resolved absolute coordinate.
    /// </summary>
    public bool HasAbsolutePosition => _hasAbsolutePosition;

    /// <summary>
    /// True when the user is hovering over this pin.
    /// </summary>
    public bool IsHovered
    {
        get => _isHovered;
        set
        {
            Set(ref _isHovered, value);
            RaisePropertyChanged(nameof(VisualScale));
        }
    }

    /// <summary>
    /// True if this pin has at least one connection.
    /// </summary>
    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            Set(ref _isConnected, value);
            RaisePropertyChanged(nameof(PinFillBrush));
        }
    }

    /// <summary>
    /// True if this pin is a valid drop target during pin drag interaction.
    /// </summary>
    public bool IsDropTarget
    {
        get => _isDropTarget;
        set
        {
            Set(ref _isDropTarget, value);
            RaisePropertyChanged(nameof(DropTargetBrush));
            RaisePropertyChanged(nameof(VisualScale));
        }
    }

    public bool IsDragIncompatible
    {
        get => _isDragIncompatible;
        set
        {
            Set(ref _isDragIncompatible, value);
            RaisePropertyChanged(nameof(VisualOpacity));
        }
    }

    /// <summary>
    /// Scale factor for visual feedback: 1.4 when hovered/drop target, 1.0 otherwise.
    /// </summary>
    public double VisualScale => IsHovered || IsDropTarget ? 1.4 : 1.0;

    public double VisualOpacity => IsDragIncompatible ? 0.2 : 1.0;

    /// <summary>
    /// Brush color when this pin is a drop target (highlighted in yellow).
    /// </summary>
    public SolidColorBrush DropTargetBrush =>
        IsDropTarget ? new SolidColorBrush(Color.Parse("#FACC15")) : PinBrush;

    /// <summary>
    /// Determines if another pin can be connected to this pin.
    /// Checks: different nodes, opposite directions, and compatible types.
    /// </summary>
    public bool CanAccept(PinViewModel other)
    {
        // Cannot connect a pin to itself (same node)
        if (other.Owner == Owner)
            return false;

        // Cannot connect same direction (both input or both output)
        if (other.Direction == Direction)
            return false;

        var src = other.Direction == PinDirection.Output ? other.EffectiveDataType : EffectiveDataType;
        var dst = other.Direction == PinDirection.Output ? EffectiveDataType : other.EffectiveDataType;

        if (IsStructuralMismatch(src, dst))
            return false;

        if (src == PinDataType.ColumnRef && dst == PinDataType.ColumnRef)
        {
            PinViewModel srcPin = other.Direction == PinDirection.Output ? other : this;
            PinViewModel dstPin = other.Direction == PinDirection.Output ? this : other;

            PinDataType? srcScalar = ResolveColumnScalarType(srcPin);
            PinDataType? dstScalar = ResolveColumnScalarType(dstPin);
            if (srcScalar is not null && dstScalar is not null && srcScalar != dstScalar)
                return false;

            return true;
        }

        if (src == PinDataType.ColumnRef && (dst.IsScalar() || dst == PinDataType.Expression))
            return true;

        if (dst == PinDataType.ColumnRef && (src.IsScalar() || src == PinDataType.Expression))
            return true;

        if (src == PinDataType.Expression && dst.IsScalar())
            return true;

        if (dst == PinDataType.Expression && src.IsScalar())
            return true;

        // Keep compatibility during migration: numeric scalar family is interoperable.
        if (src.IsNumericScalar() && dst.IsNumericScalar())
            return true;

        if (src != dst)
            return false;

        return true;
    }

    private static bool IsStructuralMismatch(PinDataType from, PinDataType to)
    {
        bool fromRowSet = from == PinDataType.RowSet;
        bool toRowSet = to == PinDataType.RowSet;
        return fromRowSet != toRowSet;
    }

    private static PinDataType? ResolveColumnScalarType(PinViewModel pin) =>
        pin.ColumnRefMeta?.ScalarType ?? pin.ExpectedColumnScalarType;

    private static string BuildRowSetPreview(NodeViewModel owner)
    {
        var schemaPins = owner.OutputPins
            .Where(p => p.EffectiveDataType == PinDataType.ColumnRef)
            .ToList();

        if (schemaPins.Count == 0)
            return string.Empty;

        string firstColumns = string.Join(
            ", ",
            schemaPins.Take(4).Select(p =>
            {
                PinDataType scalar = p.ColumnRefMeta?.ScalarType
                    ?? p.ExpectedColumnScalarType
                    ?? PinDataType.Number;
                return $"{p.Name}:{GetScalarTypeLabel(scalar)}";
            })
        );

        if (schemaPins.Count > 4)
            firstColumns += ", ...";

        return $"RowSet[{schemaPins.Count}] {firstColumns}";
    }

    private static string GetScalarTypeLabel(PinDataType type) =>
        type switch
        {
            PinDataType.Text => "TXT",
            PinDataType.Integer => "INT",
            PinDataType.Decimal => "DEC",
            PinDataType.Number => "NUM",
            PinDataType.Boolean => "BOOL",
            PinDataType.DateTime => "DT",
            PinDataType.Json => "JSON",
            PinDataType.ColumnSet => "SET",
            PinDataType.RowSet => "ROWS",
            PinDataType.Expression => "SQL",
            _ => type.ToString().ToUpperInvariant(),
        };

}
