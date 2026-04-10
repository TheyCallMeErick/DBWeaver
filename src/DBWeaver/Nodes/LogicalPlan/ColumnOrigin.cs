using DBWeaver.Nodes;

namespace DBWeaver.Nodes.LogicalPlan;

public sealed record ColumnOrigin(string DatasetAlias, string ColumnName, PinDataType Type);
