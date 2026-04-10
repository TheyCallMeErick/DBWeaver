namespace DBWeaver.Nodes.Definitions;

using DBWeaver.Nodes;
using static NodeDefinitionHelpers;

/// <summary>
/// Comparison node definitions.
/// Defines nodes for equality, inequality, and range comparison operations.
/// </summary>
public static class ComparisonDefinitions
{
    public static readonly NodeDefinition CompareEquals = new(
        NodeType.Equals,
        NodeCategory.Comparison,
        "Equals (=)",
        "Tests equality",
        [
            In("left", PinDataType.ColumnRef),
            In("right", PinDataType.ColumnRef),
            Out("result", PinDataType.Boolean),
        ],
        []
    );

    public static readonly NodeDefinition NotEquals = new(
        NodeType.NotEquals,
        NodeCategory.Comparison,
        "Not Equals (<>)",
        "Tests inequality",
        [
            In("left", PinDataType.ColumnRef),
            In("right", PinDataType.ColumnRef),
            Out("result", PinDataType.Boolean),
        ],
        []
    );

    public static readonly NodeDefinition GreaterThan = new(
        NodeType.GreaterThan,
        NodeCategory.Comparison,
        "Greater Than (>)",
        "left > right",
        [
            In("left", PinDataType.ColumnRef),
            In("right", PinDataType.ColumnRef),
            Out("result", PinDataType.Boolean),
        ],
        []
    );

    public static readonly NodeDefinition GreaterOrEqual = new(
        NodeType.GreaterOrEqual,
        NodeCategory.Comparison,
        "Greater or Equal (>=)",
        "left >= right",
        [
            In("left", PinDataType.ColumnRef),
            In("right", PinDataType.ColumnRef),
            Out("result", PinDataType.Boolean),
        ],
        []
    );

    public static readonly NodeDefinition LessThan = new(
        NodeType.LessThan,
        NodeCategory.Comparison,
        "Less Than (<)",
        "left < right",
        [
            In("left", PinDataType.ColumnRef),
            In("right", PinDataType.ColumnRef),
            Out("result", PinDataType.Boolean),
        ],
        []
    );

    public static readonly NodeDefinition LessOrEqual = new(
        NodeType.LessOrEqual,
        NodeCategory.Comparison,
        "Less or Equal (<=)",
        "left <= right",
        [
            In("left", PinDataType.ColumnRef),
            In("right", PinDataType.ColumnRef),
            Out("result", PinDataType.Boolean),
        ],
        []
    );

    public static readonly NodeDefinition Between = new(
        NodeType.Between,
        NodeCategory.Comparison,
        "BETWEEN",
        "Tests if a value is within an inclusive range",
        [
            In("value", PinDataType.ColumnRef),
            In("low", PinDataType.ColumnRef),
            In("high", PinDataType.ColumnRef),
            Out("result", PinDataType.Boolean),
        ],
        []
    );

    public static readonly NodeDefinition NotBetween = new(
        NodeType.NotBetween,
        NodeCategory.Comparison,
        "NOT BETWEEN",
        "Tests if a value is outside a range",
        [
            In("value", PinDataType.ColumnRef),
            In("low", PinDataType.ColumnRef),
            In("high", PinDataType.ColumnRef),
            Out("result", PinDataType.Boolean),
        ],
        []
    );

    public static readonly NodeDefinition IsNull = new(
        NodeType.IsNull,
        NodeCategory.Comparison,
        "IS NULL",
        "Tests if a value is null",
        [In("value", PinDataType.ColumnRef), Out("result", PinDataType.Boolean)],
        []
    );

    public static readonly NodeDefinition IsNotNull = new(
        NodeType.IsNotNull,
        NodeCategory.Comparison,
        "IS NOT NULL",
        "Tests if a value is not null",
        [In("value", PinDataType.ColumnRef), Out("result", PinDataType.Boolean)],
        []
    );

    public static readonly NodeDefinition Like = new(
        NodeType.Like,
        NodeCategory.Comparison,
        "LIKE",
        "Pattern matching with wildcards",
        [In("text", PinDataType.Text), Out("result", PinDataType.Boolean)],
        [
            new(
                "pattern",
                DBWeaver.Nodes.ParameterKind.Text,
                null,
                "e.g. '%suffix' or 'prefix%'"
            ),
        ]
    );
}
