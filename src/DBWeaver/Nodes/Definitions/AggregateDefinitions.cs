namespace DBWeaver.Nodes.Definitions;

using DBWeaver.Nodes;
using static NodeDefinitionHelpers;

/// <summary>
/// Aggregate function node definitions.
/// Defines nodes for aggregating data across multiple rows.
/// </summary>
public static class AggregateDefinitions
{
    public static readonly NodeDefinition CountStar = new(
        NodeType.CountStar,
        NodeCategory.Aggregate,
        "COUNT(*)",
        "Counts all rows",
        [Out("count", PinDataType.Number)],
        []
    );

    public static readonly NodeDefinition Sum = new(
        NodeType.Sum,
        NodeCategory.Aggregate,
        "SUM",
        "Sums a numeric column",
        [In("value", PinDataType.Number), Out("total", PinDataType.Number)],
        []
    );

    public static readonly NodeDefinition Avg = new(
        NodeType.Avg,
        NodeCategory.Aggregate,
        "AVG",
        "Average of a numeric column",
        [In("value", PinDataType.Number), Out("average", PinDataType.Number)],
        []
    );

    public static readonly NodeDefinition Min = new(
        NodeType.Min,
        NodeCategory.Aggregate,
        "MIN",
        "Minimum value",
        [In("value", PinDataType.ColumnRef), Out("minimum", PinDataType.ColumnRef)],
        []
    );

    public static readonly NodeDefinition Max = new(
        NodeType.Max,
        NodeCategory.Aggregate,
        "MAX",
        "Maximum value",
        [In("value", PinDataType.ColumnRef), Out("maximum", PinDataType.ColumnRef)],
        []
    );
}
