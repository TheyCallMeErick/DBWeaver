using VisualSqlArchitect.Expressions;

namespace VisualSqlArchitect.Nodes.Definitions;

public enum ParameterKind
{
    Text,
    Number,
    Enum,
    Boolean,
    CastType,
    JsonPath,
    DateTime,
    Date,
}

/// <summary>
/// A configurable scalar value on a node — not wired via pins but set in the
/// node's property panel (e.g. ROUND precision, CAST target type, JSON path).
/// </summary>
public sealed record NodeParameter(
    string Name,
    ParameterKind Kind,
    string? DefaultValue = null,
    string? Description = null,
    IReadOnlyList<string>? EnumValues = null // for Kind == Enum
);

/// <summary>
/// Helper factory for building node definitions.
/// </summary>
public static class NodeDefinitionHelpers
{
    public static PinDescriptor In(
        string name,
        PinDataType type = PinDataType.Any,
        bool required = true,
        bool multi = false,
        string? desc = null
    ) => new(name, PinDirection.Input, type, required, desc, multi);

    public static PinDescriptor Out(
        string name,
        PinDataType type = PinDataType.Any,
        string? desc = null
    ) => new(name, PinDirection.Output, type, Description: desc);

    public static NodeParameter Param(
        string name,
        ParameterKind kind,
        string? def = null,
        string? desc = null,
        params string[] enums
    ) => new(name, kind, def, desc, enums.Length > 0 ? enums : null);
}
