namespace DBWeaver.Nodes.Definitions;

using DBWeaver.Nodes;
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
        [In("value", PinDataType.Expression), Out("result", PinDataType.Expression)],
        [
            new(
                "fallback",
                DBWeaver.Nodes.ParameterKind.Text,
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
                DBWeaver.Nodes.ParameterKind.Text,
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
        [In("value", PinDataType.Expression), Out("result", PinDataType.Expression)],
        [
            new("src", DBWeaver.Nodes.ParameterKind.Text, null, "Input value to match"),
            new(
                "dst",
                DBWeaver.Nodes.ParameterKind.Text,
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
        [In("value", PinDataType.Expression), Out("result", PinDataType.Expression)],
        [
            new(
                "targetType",
                DBWeaver.Nodes.ParameterKind.CastType,
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

    public static readonly NodeDefinition ColumnRefCast = new(
        NodeType.ColumnRefCast,
        NodeCategory.Conditional,
        "ColumnRef Cast",
        "CAST explícito de coluna",
        [In("value", PinDataType.ColumnRef), Out("result", PinDataType.Expression)],
        [
            new(
                "targetType",
                DBWeaver.Nodes.ParameterKind.CastType,
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

    public static readonly NodeDefinition ScalarFromColumn = new(
        NodeType.ScalarFromColumn,
        NodeCategory.Conditional,
        "Scalar From Column",
        "Desempacota ColumnRef para expressão escalar",
        [In("value", PinDataType.ColumnRef), Out("result", PinDataType.Expression)],
        []
    );
}
