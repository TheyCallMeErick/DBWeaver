using DBWeaver.Nodes;

namespace DBWeaver.Nodes.LogicalPlan;

public sealed record LogicalColumn(string Name, PinDataType Type, bool IsNullable = true);
