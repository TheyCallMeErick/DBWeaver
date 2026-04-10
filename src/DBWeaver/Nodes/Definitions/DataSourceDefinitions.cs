namespace DBWeaver.Nodes.Definitions;

using DBWeaver.Nodes;
using static NodeDefinitionHelpers;

/// <summary>
/// Data source node definitions.
/// Defines nodes for selecting and aliasing data sources.
/// </summary>
public static class DataSourceDefinitions
{
    public static readonly DBWeaver.Nodes.NodeDefinition Alias = new(
        NodeType.Alias,
        NodeCategory.DataSource,
        "ALIAS (AS)",
        "Renames a column or expression with AS",
        [In("expression", PinDataType.Expression), Out("result", PinDataType.ColumnRef)],
        [
            new(
                "alias",
                DBWeaver.Nodes.ParameterKind.Text,
                null,
                "New alias name (e.g. total_price)"
            ),
        ]
    );
}
