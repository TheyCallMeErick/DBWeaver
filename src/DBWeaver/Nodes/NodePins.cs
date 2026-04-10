namespace DBWeaver.Nodes;

/// <summary>
/// Direction of a pin on a node descriptor.
/// </summary>
public enum PinDirection
{
    Input,
    Output,
}

/// <summary>
/// Static descriptor for a single connection slot on a node.
/// The canvas renders one connector per PinDescriptor.
///
/// For a table DataSource node, one PinDescriptor is generated per column,
/// all of direction=Output.
/// </summary>
public sealed record PinDescriptor(
    string Name,
    PinDirection Direction,
    PinDataType DataType,
    bool IsRequired = true,
    string? Description = null,
    bool AllowMultiple = false,
    ColumnRefMeta? ColumnRefMeta = null,
    ColumnSetMeta? ColumnSetMeta = null
);
