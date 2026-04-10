using DBWeaver.Core;
using DBWeaver.Nodes;
using DBWeaver.Registry;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class SubqueryGraphFirstContractTests
{
    [Fact]
    public void SubqueryDefinitionAndReference_AreRegisteredInNodeDefinitions()
    {
        NodeDefinition definition = NodeDefinitionRegistry.Get(NodeType.SubqueryDefinition);
        NodeDefinition reference = NodeDefinitionRegistry.Get(NodeType.SubqueryReference);

        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Output && p.Name == "subquery" && p.DataType == PinDataType.RowSet);
        Assert.Contains(reference.Pins, p => p.Direction == PinDirection.Input && p.Name == "subquery" && p.DataType == PinDataType.RowSet);
        Assert.Contains(reference.Pins, p => p.Direction == PinDirection.Output && p.Name == "result" && p.DataType == PinDataType.RowSet);
    }

    [Fact]
    public void SubqueryComparisonDefinitions_ExposeStructuralSubqueryPin()
    {
        NodeDefinition exists = NodeDefinitionRegistry.Get(NodeType.SubqueryExists);
        NodeDefinition inSubquery = NodeDefinitionRegistry.Get(NodeType.SubqueryIn);
        NodeDefinition scalar = NodeDefinitionRegistry.Get(NodeType.SubqueryScalar);

        Assert.Contains(exists.Pins, p => p.Direction == PinDirection.Input && p.Name == "subquery" && p.DataType == PinDataType.RowSet);
        Assert.Contains(inSubquery.Pins, p => p.Direction == PinDirection.Input && p.Name == "subquery" && p.DataType == PinDataType.RowSet);
        Assert.Contains(scalar.Pins, p => p.Direction == PinDirection.Input && p.Name == "subquery" && p.DataType == PinDataType.RowSet);
    }

    [Fact]
    public void CompileSubqueryExists_PrefersConnectedSubqueryNodeQuery_OverLocalLegacyParameter()
    {
        NodeInstance subquerySource = new(
            "sub",
            NodeType.Subquery,
            new Dictionary<string, string>
            {
                ["query_text"] = "SELECT 42",
            },
            new Dictionary<string, string>
            {
                ["alias"] = "sq",
            });

        NodeInstance exists = new(
            "exists",
            NodeType.SubqueryExists,
            new Dictionary<string, string>(),
            new Dictionary<string, string>());

        NodeGraph graph = new()
        {
            Nodes = [subquerySource, exists],
            Connections = [new Connection("sub", "result", "exists", "subquery")],
            WhereConditions = [new WhereBinding("exists", "result")],
        };

        EmitContext context = new(DatabaseProvider.Postgres, new SqlFunctionRegistry(DatabaseProvider.Postgres));
        CompiledNodeGraph compiled = new NodeGraphCompiler(graph, context).Compile();

        string whereSql = Assert.Single(compiled.WhereExprs).Emit(context);
        Assert.Contains("SELECT 42", whereSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("legacy_should_not_be_used", whereSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompileSubqueryIn_AcceptsConnectedSubqueryReference_AsStructuralSource()
    {
        NodeInstance table = new(
            "tbl",
            NodeType.TableSource,
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            TableFullName: "public.orders");

        NodeInstance subqueryRef = new(
            "sub_ref",
            NodeType.SubqueryReference,
            new Dictionary<string, string>
            {
                ["query_text"] = "SELECT customer_id FROM vip_customers",
            },
            new Dictionary<string, string>());

        NodeInstance inNode = new(
            "in_sub",
            NodeType.SubqueryIn,
            new Dictionary<string, string>(),
            new Dictionary<string, string>());

        NodeGraph graph = new()
        {
            Nodes = [table, subqueryRef, inNode],
            Connections =
            [
                new Connection("tbl", "customer_id", "in_sub", "value"),
                new Connection("sub_ref", "result", "in_sub", "subquery"),
            ],
            WhereConditions = [new WhereBinding("in_sub", "result")],
        };

        EmitContext context = new(DatabaseProvider.Postgres, new SqlFunctionRegistry(DatabaseProvider.Postgres));
        CompiledNodeGraph compiled = new NodeGraphCompiler(graph, context).Compile();

        string whereSql = Assert.Single(compiled.WhereExprs).Emit(context);
        Assert.Contains("vip_customers", whereSql, StringComparison.OrdinalIgnoreCase);
    }
}
