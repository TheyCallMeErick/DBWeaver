namespace DBWeaver.Nodes.Definitions;

using System.Linq;
using DBWeaver.Nodes;

/// <summary>
/// Canonical phase-1 DDL nodes exposed by the registry.
/// </summary>
public static class DdlDefinitions
{
    public static readonly IReadOnlyList<NodeType> AllTypes =
    [
        NodeType.TableDefinition,
        NodeType.ColumnDefinition,
        NodeType.PrimaryKeyConstraint,
        NodeType.ForeignKeyConstraint,
        NodeType.UniqueConstraint,
        NodeType.CheckConstraint,
        NodeType.DefaultConstraint,
        NodeType.IndexDefinition,
        NodeType.CreateTableOutput,
        NodeType.ScalarTypeDefinition,
        NodeType.AlterTableOutput,
        NodeType.CreateIndexOutput,
        NodeType.AddColumnOp,
        NodeType.DropColumnOp,
        NodeType.RenameColumnOp,
        NodeType.RenameTableOp,
        NodeType.DropTableOp,
        NodeType.AlterColumnTypeOp,
            NodeType.SequenceDefinition,
            NodeType.CreateSequenceOutput,
            NodeType.CreateTableAsOutput,
    ];

    public static IReadOnlyList<NodeDefinition> All =>
        [.. AllTypes.Select(NodeDefinitionRegistry.Get)];
}
