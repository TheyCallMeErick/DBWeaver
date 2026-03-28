using VisualSqlArchitect.Expressions;
using VisualSqlArchitect.Nodes;
using static VisualSqlArchitect.Nodes.Definitions.NodeDefinitionHelpers;

namespace VisualSqlArchitect.Nodes.Definitions;

public static class LogicGateDefinitions
{
    private static readonly IReadOnlyList<VisualSqlArchitect.Nodes.NodeParameter> EmptyParams = [];

    public static readonly NodeDefinition And = new(
        NodeType.And,
        NodeCategory.LogicGate,
        "AND",
        "All conditions must be true",
        [
            // Dynamic cond_N input pins are added by NodeViewModel.
            Out("result", PinDataType.Boolean),
        ],
        EmptyParams
    );

    public static readonly NodeDefinition Or = new(
        NodeType.Or,
        NodeCategory.LogicGate,
        "OR",
        "At least one condition must be true",
        [
            // Dynamic cond_N input pins are added by NodeViewModel.
            Out("result", PinDataType.Boolean),
        ],
        EmptyParams
    );

    public static readonly NodeDefinition Not = new(
        NodeType.Not,
        NodeCategory.LogicGate,
        "NOT",
        "Negates a boolean expression",
        [In("condition", PinDataType.Boolean), Out("result", PinDataType.Boolean)],
        EmptyParams
    );
}
