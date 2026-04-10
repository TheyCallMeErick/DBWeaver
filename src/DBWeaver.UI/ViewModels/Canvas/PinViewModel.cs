using Avalonia;
using Avalonia.Media;
using DBWeaver.Nodes;
using DBWeaver.Nodes.PinTypes;
using DBWeaver.Nodes.Pins;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.ViewModels;

/// <summary>
/// Represents a Pin (Input or Output) on a Node in the visual query builder.
/// Pins are connection points where data flows between nodes.
/// </summary>
public sealed class PinViewModel(PinDescriptor d, NodeViewModel owner) : ViewModelBase
{
    private const int ColumnSetPreviewLimit = 6;
    private Point _absolutePosition;
    private bool _hasAbsolutePosition;
    private bool _isHovered,
        _isConnected,
        _isDropTarget,
        _isDragIncompatible;

    /// <summary>
    /// The unique name of this pin within its parent node.
    /// Examples: "input", "output", "columns", "cond_1"
    /// </summary>
    public string Name { get; } = d.Name;

    public DBWeaver.Nodes.Pins.PinModel? Model { get; private set; }

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

    public IReadOnlyList<ColumnSetPreviewItem> ColumnSetPreviewItems { get; } =
        BuildColumnSetPreviewItems(d.ColumnSetMeta);

    public bool HasColumnSetPreview => ColumnSetPreviewItems.Count > 0;

    public bool HasColumnSetPreviewOverflow =>
        ColumnSetMeta is { Columns.Count: > ColumnSetPreviewLimit };

    public string ColumnSetPreviewOverflowLabel
    {
        get
        {
            if (ColumnSetMeta is null)
                return string.Empty;

            int remaining = ColumnSetMeta.Columns.Count - ColumnSetPreviewItems.Count;
            return remaining > 0 ? $"+{remaining} more" : string.Empty;
        }
    }

    /// <summary>
    /// Reference to the parent node that owns this pin.
    /// </summary>
    public NodeViewModel Owner { get; } = owner;

    /// <summary>
    /// Effective type consumed by domain decisions.
    /// ColumnRef pins are narrowed to their scalar column type when metadata is available.
    /// </summary>
    public PinDataType EffectiveDataType
    {
        get
        {
            if (DataType != PinDataType.ColumnRef)
                return DataType;

            if (ColumnRefMeta is not null)
                return ColumnRefMeta.ScalarType;

            if (ExpectedColumnScalarType is PinDataType expectedScalarType)
                return expectedScalarType;

            return DataType;
        }
    }

    /// <summary>
    /// Keeps fallback ring limited to scalar-like outputs.
    /// Structural families already have distinctive geometry and should not get a background ring.
    /// </summary>
    public bool ShowOutputFallbackVisual =>
        EffectiveDataType
            is PinDataType.ColumnRef
                or PinDataType.Text
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
            ? Color.Parse(UiColorConstants.C_FBBF24)
            : Color.Parse(PinTypeRegistry.GetType(EffectiveDataType).VisualColorHex);

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

    public PinDataType? ExpectedColumnScalarType => Owner.ResolveExpectedScalarTypeForPin(this);

    internal void NotifyExpectedColumnScalarTypeChanged()
    {
        RaisePropertyChanged(nameof(ExpectedColumnScalarType));
        RaisePropertyChanged(nameof(DataTypeLabel));
        RaisePropertyChanged(nameof(TooltipTypeDetails));
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
        IsDropTarget ? new SolidColorBrush(Color.Parse(UiColorConstants.C_FACC15)) : PinBrush;

    /// <summary>
    /// Determines if another pin can be connected to this pin.
    /// Checks: different nodes, opposite directions, and compatible types.
    /// </summary>
    public PinConnectionDecision EvaluateConnection(PinViewModel other) =>
        PinDomainAdapter.CanConnect(this, other);

    public bool CanAccept(PinViewModel other)
    {
        return EvaluateConnection(other).IsAllowed;
    }

    private static PinDataType? ResolveColumnScalarType(PinViewModel pin) =>
        pin.ColumnRefMeta?.ScalarType ?? pin.ExpectedColumnScalarType;

    private static string BuildRowSetPreview(NodeViewModel owner)
    {
        var schemaPins = owner.OutputPins
            .Where(p => p.DataType == PinDataType.ColumnRef || p.ColumnRefMeta is not null)
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
            PinDataType.TableDef => "TBL",
            PinDataType.ViewDef => "VIEW",
            PinDataType.ColumnDef => "COL",
            PinDataType.Constraint => "CON",
            PinDataType.IndexDef => "IDX",
            PinDataType.TypeDef => "TYPE",
            PinDataType.SequenceDef => "SEQ",
            PinDataType.AlterOp => "ALT",
            PinDataType.ReportQuery => "RPT",
            PinDataType.Expression => "SQL",
            _ => type.ToString().ToUpperInvariant(),
        };

    private static IReadOnlyList<ColumnSetPreviewItem> BuildColumnSetPreviewItems(ColumnSetMeta? meta)
    {
        if (meta is not { Columns.Count: > 0 })
            return [];

        return meta.Columns
            .Take(ColumnSetPreviewLimit)
            .Select(c => new ColumnSetPreviewItem(c.ColumnName, c.ScalarType, GetScalarTypeLabel(c.ScalarType), c.IsNullable))
            .ToList();
    }

    public PinViewModel(DBWeaver.Nodes.Pins.PinModel model, NodeViewModel owner)
        : this(model.Descriptor, owner)
    {
        Model = model;
        if (model.ExpectedColumnScalarType is not null)
            Owner.ComparisonExpectedScalarType = model.ExpectedColumnScalarType;
    }
}

public sealed class ColumnSetPreviewItem(
    string columnName,
    PinDataType scalarType,
    string scalarLabel,
    bool isNullable)
{
    public string ColumnName { get; } = columnName;
    public PinDataType ScalarType { get; } = scalarType;
    public string ScalarLabel { get; } = scalarLabel;
    public bool IsNullable { get; } = isNullable;
    public string NullabilityLabel => IsNullable ? "NULL" : "NOT NULL";
}
