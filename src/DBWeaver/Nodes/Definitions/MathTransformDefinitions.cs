namespace DBWeaver.Nodes.Definitions;

using DBWeaver.Nodes;
using static NodeDefinitionHelpers;

/// <summary>
/// Math transformation node definitions.
/// Defines nodes for numeric operations and mathematical functions.
/// </summary>
public static class MathTransformDefinitions
{
    public static readonly NodeDefinition Round = new(
        NodeType.Round,
        NodeCategory.MathTransform,
        "ROUND",
        "Rounds a numeric value to N decimal places",
        [
            In("value", PinDataType.Number),
            In("precision", PinDataType.Number, required: false),
            Out("result", PinDataType.Number),
        ],
        [new("precision", DBWeaver.Nodes.ParameterKind.Number, "0", "Decimal places")]
    );

    public static readonly NodeDefinition Abs = new(
        NodeType.Abs,
        NodeCategory.MathTransform,
        "ABS",
        "Absolute value",
        [In("value", PinDataType.Number), Out("result", PinDataType.Number)],
        []
    );

    public static readonly NodeDefinition Ceil = new(
        NodeType.Ceil,
        NodeCategory.MathTransform,
        "CEIL",
        "Rounds up to the nearest integer",
        [In("value", PinDataType.Number), Out("result", PinDataType.Number)],
        []
    );

    public static readonly NodeDefinition Floor = new(
        NodeType.Floor,
        NodeCategory.MathTransform,
        "FLOOR",
        "Rounds down to the nearest integer",
        [In("value", PinDataType.Number), Out("result", PinDataType.Number)],
        []
    );

    public static readonly NodeDefinition Add = new(
        NodeType.Add,
        NodeCategory.MathTransform,
        "ADD (+)",
        "Adds two values",
        [
            In("a", PinDataType.Number),
            In("b", PinDataType.Number),
            Out("result", PinDataType.Number),
        ],
        []
    );

    public static readonly NodeDefinition Subtract = new(
        NodeType.Subtract,
        NodeCategory.MathTransform,
        "SUBTRACT (-)",
        "Subtracts b from a",
        [
            In("a", PinDataType.Number),
            In("b", PinDataType.Number),
            Out("result", PinDataType.Number),
        ],
        []
    );

    public static readonly NodeDefinition Multiply = new(
        NodeType.Multiply,
        NodeCategory.MathTransform,
        "MULTIPLY (*)",
        "Multiplies two values",
        [
            In("a", PinDataType.Number),
            In("b", PinDataType.Number),
            Out("result", PinDataType.Number),
        ],
        []
    );

    public static readonly NodeDefinition Divide = new(
        NodeType.Divide,
        NodeCategory.MathTransform,
        "DIVIDE (/)",
        "Divides a by b",
        [
            In("a", PinDataType.Number),
            In("b", PinDataType.Number),
            Out("result", PinDataType.Number),
        ],
        []
    );
}
