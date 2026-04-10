using DBWeaver.Expressions;
using DBWeaver.Nodes;
using static DBWeaver.Nodes.Definitions.NodeDefinitionHelpers;

namespace DBWeaver.Nodes.Definitions;

public static class LogicGateDefinitions
{
    private static readonly IReadOnlyList<DBWeaver.Nodes.NodeParameter> EmptyParams = [];

    public static readonly NodeDefinition And = new(
        NodeType.And,
        NodeCategory.LogicGate,
        "AND",
        "All conditions must be true",
        [
            In("conditions", PinDataType.Boolean, required: false, multi: true),
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
            In("conditions", PinDataType.Boolean, required: false, multi: true),
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
