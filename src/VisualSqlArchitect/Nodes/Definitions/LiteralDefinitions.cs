namespace VisualSqlArchitect.Nodes.Definitions;

using VisualSqlArchitect.Nodes;
using static NodeDefinitionHelpers;

/// <summary>
/// Literal value node definitions.
/// Defines nodes for creating constant values of different types.
/// </summary>
public static class LiteralDefinitions
{
    public static readonly NodeDefinition ValueNumber = new(
        NodeType.ValueNumber,
        NodeCategory.Literal,
        "Number",
        "Numeric literal value",
        [Out("result", PinDataType.Number)],
        [new("value", VisualSqlArchitect.Nodes.ParameterKind.Number, "0", "Numeric value")]
    );

    public static readonly NodeDefinition ValueString = new(
        NodeType.ValueString,
        NodeCategory.Literal,
        "String",
        "Text literal value",
        [Out("result", PinDataType.Text)],
        [new("value", VisualSqlArchitect.Nodes.ParameterKind.Text, "", "String value")]
    );

    public static readonly NodeDefinition ValueDateTime = new(
        NodeType.ValueDateTime,
        NodeCategory.Literal,
        "Date/DateTime",
        "Date or DateTime literal value",
        [Out("result", PinDataType.DateTime)],
        [
            new(
                "value",
                VisualSqlArchitect.Nodes.ParameterKind.DateTime,
                "",
                "Date or DateTime literal (ISO 8601) or leave empty for NULL"
            ),
        ]
    );

    public static readonly NodeDefinition ValueBoolean = new(
        NodeType.ValueBoolean,
        NodeCategory.Literal,
        "Boolean",
        "Boolean literal value (true/false)",
        [Out("result", PinDataType.Boolean)],
        [
            new(
                "value",
                VisualSqlArchitect.Nodes.ParameterKind.Enum,
                "true",
                "Boolean value",
                ["true", "false"]
            ),
        ]
    );
}
