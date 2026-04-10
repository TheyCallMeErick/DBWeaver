using Avalonia;
using DBWeaver.Core;
using DBWeaver.Metadata;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.PropertyPanel;

public class PropertyPanelDdlParameterTests
{
    [Fact]
    public void ShowNode_TableDefinition_ExposesTableParameters()
    {
        var panel = CreatePanel();
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(0, 0));

        panel.ShowNode(node);

        Assert.Contains(panel.Parameters, p => p.Name == "SchemaName");
        Assert.Contains(panel.Parameters, p => p.Name == "TableName");
        Assert.Contains(panel.Parameters, p => p.Name == "IfNotExists");
    }

    [Fact]
    public void ShowNode_ColumnDefinition_ExposesColumnParameters()
    {
        var panel = CreatePanel();
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(0, 0));

        panel.ShowNode(node);

        Assert.Contains(panel.Parameters, p => p.Name == "ColumnName");
        Assert.Contains(panel.Parameters, p => p.Name == "DataType");
        Assert.Contains(panel.Parameters, p => p.Name == "IsNullable");
    }

    [Fact]
    public void ShowNode_TableReference_OffersVisualSchemaAndTableSuggestionsFromMetadata()
    {
        var panel = CreatePanel(BuildMetadata());
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableReference), new Point(0, 0));

        panel.ShowNode(node);

        ParameterRowViewModel schemaRow = panel.Parameters.First(parameter => parameter.Name == "SchemaName");
        ParameterRowViewModel tableRow = panel.Parameters.First(parameter => parameter.Name == "TableName");
        Assert.True(schemaRow.HasSuggestedValues);
        Assert.Contains("public", schemaRow.SuggestedValues);
        Assert.Contains("sales", schemaRow.SuggestedValues);

        Assert.True(tableRow.HasSuggestedValues);
        Assert.Contains("customers", tableRow.SuggestedValues);
        Assert.DoesNotContain("orders", tableRow.SuggestedValues);

        schemaRow.Value = "sales";
        Assert.Contains("orders", tableRow.SuggestedValues);
        Assert.DoesNotContain("customers", tableRow.SuggestedValues);
    }

    [Fact]
    public void ShowNode_TableSource_OffersQualifiedTableSuggestions()
    {
        var panel = CreatePanel(BuildMetadata());
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableSource), new Point(0, 0));

        panel.ShowNode(node);

        ParameterRowViewModel fullNameRow = panel.Parameters.First(parameter => parameter.Name == "table_full_name");
        Assert.True(fullNameRow.HasSuggestedValues);
        Assert.Contains("public.customers", fullNameRow.SuggestedValues);
        Assert.Contains("sales.orders", fullNameRow.SuggestedValues);
    }

    [Fact]
    public void ShowNode_ViewReference_OffersViewSuggestions()
    {
        var panel = CreatePanel(BuildMetadata());
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ViewReference), new Point(0, 0));

        panel.ShowNode(node);

        ParameterRowViewModel viewRow = panel.Parameters.First(parameter => parameter.Name == "ViewName");
        Assert.True(viewRow.HasSuggestedValues);
        Assert.Contains("active_orders", viewRow.SuggestedValues);
        Assert.DoesNotContain("customers", viewRow.SuggestedValues);
    }

    [Fact]
    public void ShowNode_CreateTableAsOutput_OffersSchemaAndTableSuggestions()
    {
        var panel = CreatePanel(BuildMetadata());
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.CreateTableAsOutput), new Point(0, 0));

        panel.ShowNode(node);

        ParameterRowViewModel schemaRow = panel.Parameters.First(parameter => parameter.Name == "Schema");
        ParameterRowViewModel tableRow = panel.Parameters.First(parameter => parameter.Name == "TableName");
        Assert.True(schemaRow.HasSuggestedValues);
        Assert.Contains("public", schemaRow.SuggestedValues);
        Assert.Contains("sales", schemaRow.SuggestedValues);

        Assert.True(tableRow.HasSuggestedValues);
        Assert.Contains("customers", tableRow.SuggestedValues);
        Assert.DoesNotContain("orders", tableRow.SuggestedValues);

        schemaRow.Value = "sales";
        Assert.Contains("orders", tableRow.SuggestedValues);
        Assert.DoesNotContain("customers", tableRow.SuggestedValues);
    }

    [Fact]
    public void ShowNode_RenameTableOp_OffersNewSchemaAndNameSuggestions()
    {
        var panel = CreatePanel(BuildMetadata());
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.RenameTableOp), new Point(0, 0));

        panel.ShowNode(node);

        ParameterRowViewModel newSchemaRow = panel.Parameters.First(parameter => parameter.Name == "NewSchema");
        ParameterRowViewModel newNameRow = panel.Parameters.First(parameter => parameter.Name == "NewName");
        Assert.True(newSchemaRow.HasSuggestedValues);
        Assert.Contains("public", newSchemaRow.SuggestedValues);
        Assert.Contains("sales", newSchemaRow.SuggestedValues);

        Assert.True(newNameRow.HasSuggestedValues);
        Assert.Contains("customers", newNameRow.SuggestedValues);
        Assert.Contains("orders", newNameRow.SuggestedValues);

        newSchemaRow.Value = "sales";
        Assert.Contains("orders", newNameRow.SuggestedValues);
        Assert.DoesNotContain("customers", newNameRow.SuggestedValues);
    }

    private static PropertyPanelViewModel CreatePanel(DbMetadata? metadata = null)
    {
        var canvas = new CanvasViewModel();
        var undo = new UndoRedoStack(canvas);
        return new PropertyPanelViewModel(undo, metadataResolver: () => metadata);
    }

    private static DbMetadata BuildMetadata()
    {
        TableMetadata customers = BuildTable("public", "customers", TableKind.Table);
        TableMetadata orders = BuildTable("sales", "orders", TableKind.Table);
        TableMetadata activeOrdersView = BuildTable("public", "active_orders", TableKind.View);

        return new DbMetadata(
            DatabaseName: "test-db",
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "16",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas:
            [
                new SchemaMetadata("public", [customers, activeOrdersView]),
                new SchemaMetadata("sales", [orders]),
            ],
            AllForeignKeys: [],
            Sequences: []);
    }

    private static TableMetadata BuildTable(string schema, string name, TableKind kind)
    {
        return new TableMetadata(
            Schema: schema,
            Name: name,
            Kind: kind,
            EstimatedRowCount: null,
            Columns:
            [
                new ColumnMetadata(
                    Name: "id",
                    DataType: "int",
                    NativeType: "int4",
                    IsNullable: false,
                    IsPrimaryKey: true,
                    IsForeignKey: false,
                    IsUnique: true,
                    IsIndexed: true,
                    OrdinalPosition: 1),
            ],
            Indexes: [],
            OutboundForeignKeys: [],
            InboundForeignKeys: [],
            Comment: null);
    }
}
