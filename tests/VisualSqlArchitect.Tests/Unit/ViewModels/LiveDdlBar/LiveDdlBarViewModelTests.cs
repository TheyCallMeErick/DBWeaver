using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas.Strategies;

namespace DBWeaver.Tests.Unit.ViewModels.LiveDdlBar;

public class LiveDdlBarViewModelTests
{
    [Fact]
    public void Recompile_ProducesCreateTableSql_ForConnectedDdlGraph()
    {
        var ddlCanvas = new CanvasViewModel(null, null, null, null, new DdlDomainStrategy());

        var table = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(0, 0));
        table.Parameters["SchemaName"] = "dbo";
        table.Parameters["TableName"] = "orders";
        table.Parameters["IfNotExists"] = "true";

        var col = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(0, 0));
        col.Parameters["ColumnName"] = "id";
        col.Parameters["DataType"] = "INT";
        col.Parameters["IsNullable"] = "false";

        var output = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.CreateTableOutput), new Point(0, 0));

        ddlCanvas.Nodes.Add(table);
        ddlCanvas.Nodes.Add(col);
        ddlCanvas.Nodes.Add(output);

        ddlCanvas.Connections.Add(new ConnectionViewModel(
            col.OutputPins.First(p => p.Name == "column"),
            new Point(0, 0),
            new Point(10, 10)
        ) { ToPin = table.InputPins.First(p => p.Name == "column") });

        ddlCanvas.Connections.Add(new ConnectionViewModel(
            table.OutputPins.First(p => p.Name == "table"),
            new Point(0, 0),
            new Point(10, 10)
        ) { ToPin = output.InputPins.First(p => p.Name == "table") });

        Assert.NotNull(ddlCanvas.LiveDdl);
        ddlCanvas.LiveDdl!.Recompile();

        Assert.True(ddlCanvas.LiveDdl.IsValid);
        Assert.Contains("CREATE TABLE [dbo].[orders]", ddlCanvas.LiveDdl.RawSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Recompile_GeneratesSql_WhenOnlyDefinitionsExist_AndNoOutputNode()
    {
        var ddlCanvas = new CanvasViewModel(null, null, null, null, new DdlDomainStrategy());

        var table = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(0, 0));
        table.Parameters["SchemaName"] = "dbo";
        table.Parameters["TableName"] = "orders";

        var col = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(0, 0));
        col.Parameters["ColumnName"] = "id";
        col.Parameters["DataType"] = "INT";
        col.Parameters["IsNullable"] = "false";

        ddlCanvas.Nodes.Add(table);
        ddlCanvas.Nodes.Add(col);

        ddlCanvas.Connections.Add(new ConnectionViewModel(
            col.OutputPins.First(p => p.Name == "column"),
            new Point(0, 0),
            new Point(10, 10)
        ) { ToPin = table.InputPins.First(p => p.Name == "column") });

        Assert.NotNull(ddlCanvas.LiveDdl);
        ddlCanvas.LiveDdl!.Recompile();

        Assert.True(ddlCanvas.LiveDdl.IsValid);
        Assert.Contains("CREATE TABLE [dbo].[orders]", ddlCanvas.LiveDdl.RawSql, StringComparison.OrdinalIgnoreCase);
    }
}
