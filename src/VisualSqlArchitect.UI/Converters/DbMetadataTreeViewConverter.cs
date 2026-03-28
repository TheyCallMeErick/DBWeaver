using System.Collections.ObjectModel;
using VisualSqlArchitect.Metadata;

namespace VisualSqlArchitect.UI.Converters;

/// <summary>
/// Converts DbMetadata into a TreeView-friendly structure.
/// Groups tables by schema and displays columns within each table.
/// </summary>
public static class DbMetadataTreeViewConverter
{
    public class SchemaTreeNode
    {
        public string SchemaName { get; set; } = "";
        public ObservableCollection<TableTreeNode> Tables { get; } = [];
    }

    public class TableTreeNode
    {
        public string TableName { get; set; } = "";
        public string FullName { get; set; } = "";
        public ObservableCollection<ColumnTreeNode> Columns { get; } = [];
    }

    public class ColumnTreeNode
    {
        public string ColumnName { get; set; } = "";
        public string DataType { get; set; } = "";
        public bool IsPrimaryKey { get; set; }
        public bool IsForeignKey { get; set; }
    }

    /// <summary>
    /// Converts DbMetadata to a hierarchical structure for TreeView binding.
    /// </summary>
    public static ObservableCollection<SchemaTreeNode> ToTreeViewItems(DbMetadata? metadata)
    {
        var result = new ObservableCollection<SchemaTreeNode>();

        if (metadata is null)
            return result;

        foreach (var schema in metadata.Schemas)
        {
            var schemaNode = new SchemaTreeNode { SchemaName = schema.Name };

            foreach (var table in schema.Tables.OrderBy(t => t.Name))
            {
                var tableNode = new TableTreeNode
                {
                    TableName = table.Name,
                    FullName = $"{table.Schema}.{table.Name}"
                };

                foreach (var column in table.Columns.OrderBy(c => c.OrdinalPosition))
                {
                    tableNode.Columns.Add(new ColumnTreeNode
                    {
                        ColumnName = column.Name,
                        DataType = column.NativeType,
                        IsPrimaryKey = column.IsPrimaryKey,
                        IsForeignKey = column.IsForeignKey
                    });
                }

                schemaNode.Tables.Add(tableNode);
            }

            result.Add(schemaNode);
        }

        return result;
    }
}
