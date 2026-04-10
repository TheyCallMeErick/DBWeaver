using DBWeaver.Nodes;

namespace DBWeaver.Nodes.LogicalPlan;

public sealed record ColumnDef(string Name, PinDataType Type, string SourceDataset, bool IsNullable = true);
