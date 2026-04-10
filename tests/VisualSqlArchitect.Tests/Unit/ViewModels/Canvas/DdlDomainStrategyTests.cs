using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Metadata;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas.Strategies;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class DdlDomainStrategyTests
{
    [Fact]
    public void CanEnterSubEditor_TrueOnlyForViewDefinition()
    {
        var strategy = new DdlDomainStrategy();
        var viewNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ViewDefinition), new Point(0, 0));
        var tableNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(0, 0));

        Assert.True(strategy.CanEnterSubEditor(viewNode));
        Assert.False(strategy.CanEnterSubEditor(tableNode));
    }

    [Fact]
    public void OnConnectionEstablished_SyncsTableDefinitionColumns()
    {
        var strategy = new DdlDomainStrategy();
        var table = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(0, 0));
        var column = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(40, 0));
        column.Parameters["ColumnName"] = "customer_id";
        column.Parameters["DataType"] = "INT";
        column.Parameters["IsNullable"] = "false";

        PinViewModel from = column.OutputPins.First(p => p.Name == "column");
        PinViewModel to = table.InputPins.First(p => p.Name == "column");
        var conn = new ConnectionViewModel(from, new Point(0, 0), new Point(10, 0)) { ToPin = to };

        List<NodeViewModel> nodes = [table, column];
        List<ConnectionViewModel> connections = [conn];

        strategy.OnConnectionEstablished(conn, connections, nodes);

        Assert.Single(table.TableDefinitionColumns);
        Assert.Equal("customer_id", table.TableDefinitionColumns[0].Name);
    }

    [Fact]
    public void OnConnectionRemoved_ResyncsTableDefinitionColumns()
    {
        var strategy = new DdlDomainStrategy();
        var table = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(0, 0));
        var column = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(40, 0));

        PinViewModel from = column.OutputPins.First(p => p.Name == "column");
        PinViewModel to = table.InputPins.First(p => p.Name == "column");
        var conn = new ConnectionViewModel(from, new Point(0, 0), new Point(10, 0)) { ToPin = to };

        List<NodeViewModel> nodes = [table, column];
        List<ConnectionViewModel> connections = [conn];
        strategy.OnConnectionEstablished(conn, connections, nodes);
        Assert.Single(table.TableDefinitionColumns);

        connections.Clear();
        strategy.OnConnectionRemoved(conn, connections, nodes);

        Assert.Empty(table.TableDefinitionColumns);
    }

    [Fact]
    public void GetOutputNodes_ReturnsOnlyDdlOutputs()
    {
        var strategy = new DdlDomainStrategy();
        List<NodeViewModel> nodes =
        [
            new(NodeDefinitionRegistry.Get(NodeType.CreateTableOutput), new Point(0, 0)),
            new(NodeDefinitionRegistry.Get(NodeType.AlterTableOutput), new Point(20, 0)),
            new(NodeDefinitionRegistry.Get(NodeType.CreateViewOutput), new Point(40, 0)),
            new(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(60, 0)),
        ];

        IReadOnlyList<NodeViewModel> outputs = strategy.GetOutputNodes(nodes);

        Assert.Equal(3, outputs.Count);
        Assert.DoesNotContain(outputs, n => n.Type == NodeType.TableDefinition);
    }

    [Fact]
    public void GetConnectionSuggestions_AlwaysEmpty()
    {
        var strategy = new DdlDomainStrategy();
        var owner = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(0, 0));
        var sourcePin = new PinViewModel(new PinDescriptor("column", PinDirection.Output, PinDataType.ColumnDef), owner);

        IReadOnlyList<NodeSuggestion> suggestions = strategy.GetConnectionSuggestions(sourcePin, [owner]);

        Assert.Empty(suggestions);
    }

    [Fact]
    public void TryHandleSchemaTableInsert_InDdlMode_InvokesImporter()
    {
        var strategy = new DdlDomainStrategy();
        bool imported = false;
        bool spawned = false;

        bool handled = strategy.TryHandleSchemaTableInsert(
            BuildTable(),
            new Point(120, 60),
            () => true,
            (_, _) => imported = true,
            () => spawned = true
        );

        Assert.True(handled);
        Assert.True(imported);
        Assert.False(spawned);
    }

    // â”€â”€ type_def resolution in table preview â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void TablePreview_ShowsResolvedScalarType_WhenTypeDefPinConnected()
    {
        var strategy = new DdlDomainStrategy();
        var table  = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition),  new Point(0, 0));
        var column = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(40, 0));
        var scalar = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ScalarTypeDefinition), new Point(80, 0));

        column.Parameters["ColumnName"] = "price";
        scalar.Parameters["TypeKind"]   = "DECIMAL";
        scalar.Parameters["Precision"]  = "10";
        scalar.Parameters["Scale"]      = "4";

        // scalar â†’ type_def pin of column
        PinViewModel scalarOut  = scalar.OutputPins.First(p => p.Name == "type_def");
        PinViewModel columnType = column.InputPins.First(p => p.Name == "type_def");
        var typeConn = new ConnectionViewModel(scalarOut, new Point(0, 0), new Point(10, 0)) { ToPin = columnType };

        // column â†’ column pin of table
        PinViewModel colOut  = column.OutputPins.First(p => p.Name == "column");
        PinViewModel tableIn = table.InputPins.First(p => p.Name == "column");
        var colConn = new ConnectionViewModel(colOut, new Point(0, 0), new Point(10, 0)) { ToPin = tableIn };

        List<NodeViewModel> nodes = [table, column, scalar];
        List<ConnectionViewModel> connections = [colConn, typeConn];

        strategy.OnConnectionEstablished(colConn, connections, nodes);

        Assert.Single(table.TableDefinitionColumns);
        Assert.Equal("DECIMAL(10,4)", table.TableDefinitionColumns[0].DataType);
    }

    [Fact]
    public void TablePreview_FallsBackToDataTypeParam_WhenTypeDefPinNotConnected()
    {
        var strategy = new DdlDomainStrategy();
        var table  = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition),  new Point(0, 0));
        var column = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(40, 0));
        column.Parameters["ColumnName"] = "age";
        column.Parameters["DataType"]   = "SMALLINT";

        PinViewModel from = column.OutputPins.First(p => p.Name == "column");
        PinViewModel to   = table.InputPins.First(p => p.Name == "column");
        var conn = new ConnectionViewModel(from, new Point(0, 0), new Point(10, 0)) { ToPin = to };

        strategy.OnConnectionEstablished(conn, [conn], [table, column]);

        Assert.Equal("SMALLINT", table.TableDefinitionColumns[0].DataType);
    }

    [Fact]
    public void TablePreview_ShowsVarcharWithLength_WhenScalarTypeIsVarchar()
    {
        var strategy = new DdlDomainStrategy();
        var table  = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition),  new Point(0, 0));
        var column = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(40, 0));
        var scalar = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ScalarTypeDefinition), new Point(80, 0));

        column.Parameters["ColumnName"] = "email";
        scalar.Parameters["TypeKind"]   = "VARCHAR";
        scalar.Parameters["Length"]     = "320";

        PinViewModel scalarOut  = scalar.OutputPins.First(p => p.Name == "type_def");
        PinViewModel columnType = column.InputPins.First(p => p.Name == "type_def");
        var typeConn = new ConnectionViewModel(scalarOut, new Point(0, 0), new Point(10, 0)) { ToPin = columnType };

        PinViewModel colOut  = column.OutputPins.First(p => p.Name == "column");
        PinViewModel tableIn = table.InputPins.First(p => p.Name == "column");
        var colConn = new ConnectionViewModel(colOut, new Point(0, 0), new Point(10, 0)) { ToPin = tableIn };

        strategy.OnConnectionEstablished(colConn, [colConn, typeConn], [table, column, scalar]);

        Assert.Equal("VARCHAR(320)", table.TableDefinitionColumns[0].DataType);
    }

    // â”€â”€ OnParameterChanged â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void OnParameterChanged_ReSyncsTable_WhenScalarTypeKindChanges()
    {
        var strategy = new DdlDomainStrategy();
        var table  = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition),  new Point(0, 0));
        var column = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(40, 0));
        var scalar = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ScalarTypeDefinition), new Point(80, 0));

        column.Parameters["ColumnName"] = "flag";
        scalar.Parameters["TypeKind"]   = "INT";

        PinViewModel scalarOut  = scalar.OutputPins.First(p => p.Name == "type_def");
        PinViewModel columnType = column.InputPins.First(p => p.Name == "type_def");
        var typeConn = new ConnectionViewModel(scalarOut, new Point(0, 0), new Point(10, 0)) { ToPin = columnType };

        PinViewModel colOut  = column.OutputPins.First(p => p.Name == "column");
        PinViewModel tableIn = table.InputPins.First(p => p.Name == "column");
        var colConn = new ConnectionViewModel(colOut, new Point(0, 0), new Point(10, 0)) { ToPin = tableIn };

        List<ConnectionViewModel> connections = [colConn, typeConn];
        strategy.OnConnectionEstablished(colConn, connections, [table, column, scalar]);
        Assert.Equal("INT", table.TableDefinitionColumns[0].DataType);

        // Now change the scalar type kind to BOOLEAN
        scalar.Parameters["TypeKind"] = "BOOLEAN";
        strategy.OnParameterChanged(scalar, "TypeKind", connections, [table, column, scalar]);

        Assert.Equal("BOOLEAN", table.TableDefinitionColumns[0].DataType);
    }

    [Fact]
    public void OnParameterChanged_ReSyncsTable_WhenVarcharLengthChanges()
    {
        var strategy = new DdlDomainStrategy();
        var table  = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition),  new Point(0, 0));
        var column = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(40, 0));
        var scalar = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ScalarTypeDefinition), new Point(80, 0));

        column.Parameters["ColumnName"] = "name";
        scalar.Parameters["TypeKind"]   = "VARCHAR";
        scalar.Parameters["Length"]     = "100";

        PinViewModel scalarOut  = scalar.OutputPins.First(p => p.Name == "type_def");
        PinViewModel columnType = column.InputPins.First(p => p.Name == "type_def");
        var typeConn = new ConnectionViewModel(scalarOut, new Point(0, 0), new Point(10, 0)) { ToPin = columnType };

        PinViewModel colOut  = column.OutputPins.First(p => p.Name == "column");
        PinViewModel tableIn = table.InputPins.First(p => p.Name == "column");
        var colConn = new ConnectionViewModel(colOut, new Point(0, 0), new Point(10, 0)) { ToPin = tableIn };

        List<ConnectionViewModel> connections = [colConn, typeConn];
        strategy.OnConnectionEstablished(colConn, connections, [table, column, scalar]);

        // Change length
        scalar.Parameters["Length"] = "500";
        strategy.OnParameterChanged(scalar, "Length", connections, [table, column, scalar]);

        Assert.Equal("VARCHAR(500)", table.TableDefinitionColumns[0].DataType);
    }

    [Fact]
    public void OnParameterChanged_DoesNothing_WhenNodeIsNotTypeNode()
    {
        var strategy = new DdlDomainStrategy();
        var table  = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition),  new Point(0, 0));
        var column = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(40, 0));
        column.Parameters["ColumnName"] = "id";
        column.Parameters["DataType"]   = "BIGINT";

        PinViewModel from = column.OutputPins.First(p => p.Name == "column");
        PinViewModel to   = table.InputPins.First(p => p.Name == "column");
        var conn = new ConnectionViewModel(from, new Point(0, 0), new Point(10, 0)) { ToPin = to };

        strategy.OnConnectionEstablished(conn, [conn], [table, column]);
        Assert.Equal("BIGINT", table.TableDefinitionColumns[0].DataType);

        // Changing a ColumnDefinition parameter must not trigger any crash/re-sync
        strategy.OnParameterChanged(column, "ColumnName", [conn], [table, column]);

        // Should still be the same
        Assert.Equal("BIGINT", table.TableDefinitionColumns[0].DataType);
    }

    [Fact]
    public void OnParameterChanged_DoesNothing_WhenUnrelatedScalarParamChanges()
    {
        var strategy = new DdlDomainStrategy();
        var table  = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition),  new Point(0, 0));
        var column = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(40, 0));
        var scalar = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ScalarTypeDefinition), new Point(80, 0));

        column.Parameters["ColumnName"] = "notes";
        scalar.Parameters["TypeKind"]   = "TEXT";

        PinViewModel scalarOut  = scalar.OutputPins.First(p => p.Name == "type_def");
        PinViewModel columnType = column.InputPins.First(p => p.Name == "type_def");
        var typeConn = new ConnectionViewModel(scalarOut, new Point(0, 0), new Point(10, 0)) { ToPin = columnType };

        PinViewModel colOut  = column.OutputPins.First(p => p.Name == "column");
        PinViewModel tableIn = table.InputPins.First(p => p.Name == "column");
        var colConn = new ConnectionViewModel(colOut, new Point(0, 0), new Point(10, 0)) { ToPin = tableIn };

        List<ConnectionViewModel> connections = [colConn, typeConn];
        strategy.OnConnectionEstablished(colConn, connections, [table, column, scalar]);
        Assert.Equal("TEXT", table.TableDefinitionColumns[0].DataType);

        // Changing an irrelevant param on the scalar node should be a no-op
        int columnsBefore = table.TableDefinitionColumns.Count;
        strategy.OnParameterChanged(scalar, "Comment", connections, [table, column, scalar]);

        Assert.Equal(columnsBefore, table.TableDefinitionColumns.Count);
        Assert.Equal("TEXT", table.TableDefinitionColumns[0].DataType);
    }

    private static TableMetadata BuildTable() => new(
        Schema: "public",
        Name: "orders",
        Kind: TableKind.Table,
        EstimatedRowCount: null,
        Columns: [],
        Indexes: [],
        OutboundForeignKeys: [],
        InboundForeignKeys: []
    );
}


