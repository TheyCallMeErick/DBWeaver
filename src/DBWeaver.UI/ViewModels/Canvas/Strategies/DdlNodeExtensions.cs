using DBWeaver.Nodes;

namespace DBWeaver.UI.ViewModels.Canvas.Strategies;

internal static class DdlNodeExtensions
{
    public static bool IsDdlTableDefinition(this NodeViewModel node)
        => node.Type == NodeType.TableDefinition;

    public static bool IsDdlColumnDefinition(this NodeViewModel node)
        => node.Type == NodeType.ColumnDefinition;

    public static bool IsDdlPrimaryKeyConstraint(this NodeViewModel node)
        => node.Type == NodeType.PrimaryKeyConstraint;

    public static bool IsDdlForeignKeyConstraint(this NodeViewModel node)
        => node.Type == NodeType.ForeignKeyConstraint;

    public static bool IsDdlUniqueConstraint(this NodeViewModel node)
        => node.Type == NodeType.UniqueConstraint;
}
