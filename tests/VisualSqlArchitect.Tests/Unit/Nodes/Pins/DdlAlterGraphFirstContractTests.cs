using DBWeaver.Ddl;
using DBWeaver.Nodes;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class DdlAlterGraphFirstContractTests
{
    [Fact]
    public void DdlAlterNodes_ExposeGraphFirstTargetPins()
    {
        NodeDefinition dropColumn = NodeDefinitionRegistry.Get(NodeType.DropColumnOp);
        NodeDefinition renameColumn = NodeDefinitionRegistry.Get(NodeType.RenameColumnOp);
        NodeDefinition renameTable = NodeDefinitionRegistry.Get(NodeType.RenameTableOp);
        NodeDefinition alterType = NodeDefinitionRegistry.Get(NodeType.AlterColumnTypeOp);

        Assert.Contains(dropColumn.Pins, p => p.Direction == PinDirection.Input && p.Name == "target_column" && p.DataType == PinDataType.ColumnDef);
        Assert.Contains(renameColumn.Pins, p => p.Direction == PinDirection.Input && p.Name == "target_column" && p.DataType == PinDataType.ColumnDef);
        Assert.Contains(renameColumn.Pins, p => p.Direction == PinDirection.Input && p.Name == "new_name" && p.DataType == PinDataType.Text);
        Assert.Contains(renameTable.Pins, p => p.Direction == PinDirection.Input && p.Name == "target_table" && p.DataType == PinDataType.TableDef);
        Assert.Contains(renameTable.Pins, p => p.Direction == PinDirection.Input && p.Name == "new_name" && p.DataType == PinDataType.Text);
        Assert.Contains(renameTable.Pins, p => p.Direction == PinDirection.Input && p.Name == "new_schema" && p.DataType == PinDataType.Text);
        Assert.Contains(alterType.Pins, p => p.Direction == PinDirection.Input && p.Name == "target_column" && p.DataType == PinDataType.ColumnDef);
        Assert.Contains(alterType.Pins, p => p.Direction == PinDirection.Input && p.Name == "new_column" && p.DataType == PinDataType.ColumnDef);
    }

    [Fact]
    public void CompileAlterTable_UsesWiredTargetsAndTextInputs_WhenProvided()
    {
        var graph = BuildGraphForWiredAlterOperations();
        var compiler = new DdlGraphCompiler(graph);

        DdlCompileResult result = compiler.CompileWithDiagnostics();
        AlterTableExpr alter = Assert.IsType<AlterTableExpr>(Assert.Single(result.Statements));

        DropColumnOpExpr drop = Assert.IsType<DropColumnOpExpr>(alter.Operations.Single(o => o is DropColumnOpExpr));
        RenameColumnOpExpr renameColumn = Assert.IsType<RenameColumnOpExpr>(alter.Operations.Single(o => o is RenameColumnOpExpr));
        RenameTableOpExpr renameTable = Assert.IsType<RenameTableOpExpr>(alter.Operations.Single(o => o is RenameTableOpExpr));
        AlterColumnTypeOpExpr alterType = Assert.IsType<AlterColumnTypeOpExpr>(alter.Operations.Single(o => o is AlterColumnTypeOpExpr));

        Assert.Equal("legacy_code", drop.ColumnName);
        Assert.Equal("old_name", renameColumn.OldName);
        Assert.Equal("new_name_wired", renameColumn.NewName);
        Assert.Equal("orders_v2_wired", renameTable.NewName);
        Assert.Equal("archive", renameTable.NewSchema);
        Assert.Equal("total_old", alterType.ColumnName);
        Assert.Equal("NUMERIC(10,2)", alterType.NewDataType);
    }

    private static NodeGraph BuildGraphForWiredAlterOperations()
    {
        List<NodeInstance> nodes =
        [
            new("out", NodeType.AlterTableOutput, new Dictionary<string, string>(), new Dictionary<string, string>()),
            new("table", NodeType.TableDefinition, new Dictionary<string, string>(), new Dictionary<string, string>
            {
                ["SchemaName"] = "public",
                ["TableName"] = "orders",
            }),
            new("drop", NodeType.DropColumnOp, new Dictionary<string, string>(), new Dictionary<string, string>
            {
                ["ColumnName"] = "legacy_fallback",
            }),
            new("rename_col", NodeType.RenameColumnOp, new Dictionary<string, string>(), new Dictionary<string, string>
            {
                ["OldName"] = "old_name_fallback",
                ["NewName"] = "new_name_fallback",
            }),
            new("rename_table", NodeType.RenameTableOp, new Dictionary<string, string>(), new Dictionary<string, string>
            {
                ["NewName"] = "orders_v2_fallback",
                ["NewSchema"] = "public",
            }),
            new("alter_type", NodeType.AlterColumnTypeOp, new Dictionary<string, string>(), new Dictionary<string, string>()),
            new("target_drop", NodeType.ColumnDefinition, new Dictionary<string, string>(), new Dictionary<string, string>
            {
                ["ColumnName"] = "legacy_code",
                ["DataType"] = "TEXT",
                ["IsNullable"] = "true",
            }),
            new("target_rename", NodeType.ColumnDefinition, new Dictionary<string, string>(), new Dictionary<string, string>
            {
                ["ColumnName"] = "old_name",
                ["DataType"] = "TEXT",
                ["IsNullable"] = "true",
            }),
            new("target_alter", NodeType.ColumnDefinition, new Dictionary<string, string>(), new Dictionary<string, string>
            {
                ["ColumnName"] = "total_old",
                ["DataType"] = "INT",
                ["IsNullable"] = "false",
            }),
            new("new_col_spec", NodeType.ColumnDefinition, new Dictionary<string, string>(), new Dictionary<string, string>
            {
                ["ColumnName"] = "total_new",
                ["DataType"] = "NUMERIC(10,2)",
                ["IsNullable"] = "true",
            }),
            new("name_value", NodeType.ValueString, new Dictionary<string, string>(), new Dictionary<string, string>
            {
                ["value"] = "new_name_wired",
            }),
            new("table_name_value", NodeType.ValueString, new Dictionary<string, string>(), new Dictionary<string, string>
            {
                ["value"] = "orders_v2_wired",
            }),
            new("table_schema_value", NodeType.ValueString, new Dictionary<string, string>(), new Dictionary<string, string>
            {
                ["value"] = "archive",
            }),
        ];

        List<Connection> connections =
        [
            new("table", "table", "out", "table"),
            new("drop", "op", "out", "operation"),
            new("rename_col", "op", "out", "operation"),
            new("rename_table", "op", "out", "operation"),
            new("alter_type", "op", "out", "operation"),
            new("target_drop", "column", "drop", "target_column"),
            new("target_rename", "column", "rename_col", "target_column"),
            new("name_value", "result", "rename_col", "new_name"),
            new("table", "table", "rename_table", "target_table"),
            new("table_name_value", "result", "rename_table", "new_name"),
            new("table_schema_value", "result", "rename_table", "new_schema"),
            new("target_alter", "column", "alter_type", "target_column"),
            new("new_col_spec", "column", "alter_type", "new_column"),
        ];

        return new NodeGraph
        {
            Nodes = nodes,
            Connections = connections,
        };
    }
}
