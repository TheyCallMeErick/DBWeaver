namespace DBWeaver.Nodes.PinTypes;

/// <summary>
/// Defines the visual dash style for wires carrying a specific pin data type.
/// </summary>
public enum PinWireDashKind
{
    /// <summary>Solid line (default).</summary>
    Solid,
    /// <summary>Short dashes.</summary>
    ShortDash,
    /// <summary>Medium-length dashes.</summary>
    MediumDash,
    /// <summary>Long dashes.</summary>
    LongDash,
    /// <summary>Very long dashes.</summary>
    WideDash,
    /// <summary>Dotted line.</summary>
    Dotted,
}

/// <summary>
/// Represents a transport type used by pins in the canvas graph.
/// Encapsulates visual properties (color, thickness, dash style) and compatibility rules.
/// Replaces the legacy <see cref="PinDataType"/> enum, enabling domain-specific pin type extensions.
/// </summary>
public interface IPinDataType
{
    /// <summary>Gets the human-readable name of this pin type (e.g., "Text", "ColumnDef").</summary>
    string Name { get; }

    /// <summary>Gets the hexadecimal color code for visualizing pins and wires of this type.</summary>
    string VisualColorHex { get; }

    /// <summary>Gets the thickness (in pixels or DPI-adjusted units) of wires carrying this pin type.</summary>
    double WireThickness { get; }

    /// <summary>Gets the dash kind for wires carrying this pin type.</summary>
    PinWireDashKind WireDashKind { get; }

    /// <summary>
    /// Returns true when this target type can accept data from the provided source type.
    /// Used during connection validation to enforce domain-specific type compatibility.
    /// For example, Query types reject DDL types and vice versa.
    /// </summary>
    /// <param name="source">The source pin type attempting to connect.</param>
    /// <returns>True if this type can receive data from the source type; otherwise false.</returns>
    bool CanReceiveFrom(IPinDataType source);
}
