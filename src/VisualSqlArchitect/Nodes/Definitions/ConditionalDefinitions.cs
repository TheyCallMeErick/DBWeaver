namespace VisualSqlArchitect.Nodes.Definitions;

using VisualSqlArchitect.Nodes;
using static NodeDefinitionHelpers;

/// <summary>
/// Conditional and value transformation node definitions.
/// Defines nodes for conditional logic and value mapping operations.
/// </summary>
public static class ConditionalDefinitions
{
    public static readonly NodeDefinition NullFill = new(
        NodeType.NullFill,
        NodeCategory.Conditional,
        "NULL Fill",
        "Returns a fallback value when input is NULL — COALESCE(value, fallback)",
        [In("value", PinDataType.Any), Out("result", PinDataType.Any)],
        [
            new(
                "fallback",
                VisualSqlArchitect.Nodes.ParameterKind.Text,
                "",
                "Value returned when input is NULL"
            ),
        ]
    );

    public static readonly NodeDefinition EmptyFill = new(
        NodeType.EmptyFill,
        NodeCategory.Conditional,
        "Empty Fill",
        "Returns a fallback when input is NULL or an empty/whitespace string",
        [In("value", PinDataType.Text), Out("result", PinDataType.Text)],
        [
            new(
                "fallback",
                VisualSqlArchitect.Nodes.ParameterKind.Text,
                "",
                "Value returned when input is NULL or empty"
            ),
        ]
    );

    public static readonly NodeDefinition ValueMap = new(
        NodeType.ValueMap,
        NodeCategory.Conditional,
        "Value Map",
        "Maps a specific input value to a new output value — CASE WHEN value = src THEN dst ELSE passthrough",
        [In("value", PinDataType.Any), Out("result", PinDataType.Any)],
        [
            new("src", VisualSqlArchitect.Nodes.ParameterKind.Text, null, "Input value to match"),
            new(
                "dst",
                VisualSqlArchitect.Nodes.ParameterKind.Text,
                null,
                "Output value when matched"
            ),
        ]
    );

    public static readonly NodeDefinition Cast = new(
        NodeType.Cast,
        NodeCategory.Conditional,
        "CAST",
        "Converts a value to another data type",
        [In("value", PinDataType.Any), Out("result", PinDataType.Any)],
        [
            new(
                "targetType",
                VisualSqlArchitect.Nodes.ParameterKind.CastType,
                "Text",
                "Target SQL type",
                [
                    "Text",
                    "Integer",
                    "BigInt",
                    "Decimal",
                    "Float",
                    "Boolean",
                    "Date",
                    "DateTime",
                    "Timestamp",
                    "Uuid",
                ]
            ),
        ]
    );
}
