using DBWeaver.Core;
using DBWeaver.Ddl;
using DBWeaver.Nodes;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class ViewDefinitionGraphFirstContractTests
{
    [Fact]
    public void ViewDefinition_Definition_ExposesTextInputsForGraphFirstConfiguration()
    {
        NodeDefinition definition = NodeDefinitionRegistry.Get(NodeType.ViewDefinition);

        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Input && p.Name == "schema_text" && p.DataType == PinDataType.Text);
        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Input && p.Name == "view_name_text" && p.DataType == PinDataType.Text);
        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Input && p.Name == "from_table_text" && p.DataType == PinDataType.Text);
        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Input && p.Name == "select_sql_text" && p.DataType == PinDataType.Text);
    }

    [Fact]
    public void CompileCreateView_UsesWiredTextInputs_WhenProvided()
    {
        NodeGraph graph = new()
        {
            Nodes =
            [
                new("out", NodeType.CreateViewOutput, new Dictionary<string, string>(), new Dictionary<string, string>()),
                new("view", NodeType.ViewDefinition, new Dictionary<string, string>(), new Dictionary<string, string>
                {
                    ["Schema"] = "legacy_schema",
                    ["ViewName"] = "legacy_view",
                    ["SelectSql"] = "SELECT legacy_value",
                }),
                new("schema", NodeType.ValueString, new Dictionary<string, string>(), new Dictionary<string, string> { ["value"] = "reporting" }),
                new("name", NodeType.ValueString, new Dictionary<string, string>(), new Dictionary<string, string> { ["value"] = "vw_orders" }),
                new("select", NodeType.ValueString, new Dictionary<string, string>(), new Dictionary<string, string> { ["value"] = "SELECT 1 AS id" }),
            ],
            Connections =
            [
                new("view", "view", "out", "view"),
                new("schema", "result", "view", "schema_text"),
                new("name", "result", "view", "view_name_text"),
                new("select", "result", "view", "select_sql_text"),
            ],
        };

        DdlCompileResult result = new DdlGraphCompiler(graph, DatabaseProvider.Postgres).CompileWithDiagnostics();
        CreateViewExpr expression = Assert.IsType<CreateViewExpr>(Assert.Single(result.Statements));

        Assert.Equal("reporting", expression.SchemaName);
        Assert.Equal("vw_orders", expression.ViewName);
        Assert.Equal("SELECT 1 AS id", expression.SelectSql);
    }
}
