using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas.Strategies;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class DdlPhase3TableDefinitionViewModelTests
{
    [Fact]
    public void DdlTableColumns_ProjectConstraintFlagsFromConnectedNodes()
    {
        var canvas = new CanvasViewModel(null, null, null, null, new DdlDomainStrategy());
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        var table = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(0, 0));

        var idColumn = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(0, 0));
        idColumn.Parameters["ColumnName"] = "id";
        idColumn.Parameters["DataType"] = "INT";
        idColumn.Parameters["IsNullable"] = "false";

        var customerIdColumn = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(0, 0));
        customerIdColumn.Parameters["ColumnName"] = "customer_id";
        customerIdColumn.Parameters["DataType"] = "INT";
        customerIdColumn.Parameters["IsNullable"] = "true";

        var primaryKey = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.PrimaryKeyConstraint), new Point(0, 0));
        var unique = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.UniqueConstraint), new Point(0, 0));
        var foreignKey = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ForeignKeyConstraint), new Point(0, 0));

        canvas.Nodes.Add(table);
        canvas.Nodes.Add(idColumn);
        canvas.Nodes.Add(customerIdColumn);
        canvas.Nodes.Add(primaryKey);
        canvas.Nodes.Add(unique);
        canvas.Nodes.Add(foreignKey);

        Connect(canvas,
            idColumn.OutputPins.First(p => p.Name == "column"),
            table.InputPins.First(p => p.Name == "column")
        );
        Connect(canvas,
            customerIdColumn.OutputPins.First(p => p.Name == "column"),
            table.InputPins.First(p => p.Name == "column")
        );

        Connect(canvas,
            idColumn.OutputPins.First(p => p.Name == "column"),
            primaryKey.InputPins.First(p => p.Name == "column")
        );
        Connect(canvas,
            primaryKey.OutputPins.First(p => p.Name == "pk"),
            table.InputPins.First(p => p.Name == "constraint")
        );

        Connect(canvas,
            idColumn.OutputPins.First(p => p.Name == "column"),
            unique.InputPins.First(p => p.Name == "column")
        );
        Connect(canvas,
            unique.OutputPins.First(p => p.Name == "uq"),
            table.InputPins.First(p => p.Name == "constraint")
        );

        Connect(canvas,
            customerIdColumn.OutputPins.First(p => p.Name == "column"),
            foreignKey.InputPins.First(p => p.Name == "child_column")
        );
        Connect(canvas,
            foreignKey.OutputPins.First(p => p.Name == "fk"),
            table.InputPins.First(p => p.Name == "constraint")
        );

        Assert.Equal(2, table.TableDefinitionColumns.Count);

        var idRow = table.TableDefinitionColumns.First(r => r.Name == "id");
        Assert.True(idRow.IsPrimaryKey);
        Assert.True(idRow.IsUnique);
        Assert.False(idRow.IsForeignKey);
        Assert.False(idRow.IsNullable);

        var customerRow = table.TableDefinitionColumns.First(r => r.Name == "customer_id");
        Assert.False(customerRow.IsPrimaryKey);
        Assert.False(customerRow.IsUnique);
        Assert.True(customerRow.IsForeignKey);
        Assert.True(customerRow.IsNullable);
    }

    [Fact]
    public void DdlTableDisplayName_UsesSchemaAndTableParameters()
    {
        var table = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(0, 0));
        table.Parameters["SchemaName"] = "sales";
        table.Parameters["TableName"] = "orders";

        Assert.Equal("sales.orders", table.TableDefinitionDisplayName);
    }

    private static void Connect(CanvasViewModel canvas, PinViewModel from, PinViewModel to)
    {
        var conn = new ConnectionViewModel(from, new Point(0, 0), new Point(10, 10))
        {
            ToPin = to,
        };

        canvas.Connections.Add(conn);
    }
}


