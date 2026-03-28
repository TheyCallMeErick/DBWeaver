namespace VisualSqlArchitect.Nodes.Definitions;

using VisualSqlArchitect.Nodes;
using static NodeDefinitionHelpers;

/// <summary>
/// Data source node definitions.
/// Defines nodes for selecting and aliasing data sources.
/// </summary>
public static class DataSourceDefinitions
{
    public static readonly VisualSqlArchitect.Nodes.NodeDefinition Alias = new(
        NodeType.Alias,
        NodeCategory.DataSource,
        "ALIAS (AS)",
        "Renames a column or expression with AS",
        [In("expression", PinDataType.Any), Out("result", PinDataType.Any)],
        [
            new(
                "alias",
                VisualSqlArchitect.Nodes.ParameterKind.Text,
                null,
                "New alias name (e.g. total_price)"
            ),
        ]
    );
}
