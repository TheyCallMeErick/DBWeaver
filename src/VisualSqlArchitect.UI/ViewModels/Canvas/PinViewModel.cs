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
    private bool _isHovered,
        _isConnected,
        _isDropTarget;
    private PinDataType? _narrowedDataType;

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
            RaisePropertyChanged(nameof(DataTypeLabel));
        }
    }

    /// <summary>
    /// Returns the effective data type: narrowed type if set, otherwise the original DataType.
    /// </summary>
    public PinDataType EffectiveDataType => NarrowedDataType ?? DataType;

    /// <summary>
    /// Color representation of this pin's data type for UI visualization.
    /// </summary>
    public Color PinColor =>
        EffectiveDataType switch
        {
            PinDataType.Text => Color.Parse("#60A5FA"),
            PinDataType.Number => Color.Parse("#4ADE80"),
            PinDataType.Boolean => Color.Parse("#FACC15"),
            PinDataType.DateTime => Color.Parse("#22D3EE"),
            PinDataType.Json => Color.Parse("#A78BFA"),
            PinDataType.Expression => Color.Parse("#F97316"),
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

    /// <summary>
    /// Display label for the data type (e.g., "TEXT", "NUMBER").
    /// </summary>
    public string DataTypeLabel => EffectiveDataType.ToString().ToUpperInvariant();

    /// <summary>
    /// Absolute screen position of this pin for wire drawing.
    /// Updated by InfiniteCanvas.UpdatePinPositions().
    /// </summary>
    public Point AbsolutePosition
    {
        get => _absolutePosition;
        set => Set(ref _absolutePosition, value);
    }

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
        set => Set(ref _isConnected, value);
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

    /// <summary>
    /// Scale factor for visual feedback: 1.4 when hovered/drop target, 1.0 otherwise.
    /// </summary>
    public double VisualScale => IsHovered || IsDropTarget ? 1.4 : 1.0;

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

        // Cannot connect incompatible types (unless one is "Any")
        if (
            EffectiveDataType != PinDataType.Any
            && other.EffectiveDataType != PinDataType.Any
            && EffectiveDataType != other.EffectiveDataType
        )
            return false;

        return true;
    }
}
