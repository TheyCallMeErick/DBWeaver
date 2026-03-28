namespace VisualSqlArchitect.Nodes.Definitions;

using VisualSqlArchitect.Nodes;
using static NodeDefinitionHelpers;

/// <summary>
/// Result modifier node definitions.
/// Defines nodes for modifying query results (limits, where clauses, etc).
/// </summary>
public static class ResultModifierDefinitions
{
    public static readonly NodeDefinition Top = new(
        NodeType.Top,
        NodeCategory.ResultModifier,
        "TOP / LIMIT",
        "Limits the number of rows returned from a query",
        [
            In(
                "count",
                PinDataType.Number,
                required: false,
                desc: "Connect a Number node or set manually"
            ),
            Out("result", PinDataType.Any),
        ],
        [
            new(
                "count",
                VisualSqlArchitect.Nodes.ParameterKind.Number,
                "100",
                "Maximum number of rows to return"
            ),
        ]
    );

    public static readonly NodeDefinition CompileWhere = new(
        NodeType.CompileWhere,
        NodeCategory.ResultModifier,
        "COMPILE WHERE",
        "Combines multiple boolean conditions into a WHERE clause",
        [
            In(
                "conditions",
                PinDataType.Boolean,
                required: false,
                multi: true,
                desc: "Connect boolean comparisons/expressions"
            ),
            Out(
                "result",
                PinDataType.Boolean,
                desc: "Connect to ResultOutput to generate WHERE clause"
            ),
        ],
        []
    );
}
