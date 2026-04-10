using DBWeaver.Nodes;
using DBWeaver.Registry;
using DBWeaver.Core;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class NodeGraphCompilerGraphFirstContractTests
{
    [Fact]
    public void OutputDefinitions_ExposeGraphFirstOrderAndGroupPins()
    {
        NodeDefinition selectOutput = NodeDefinitionRegistry.Get(NodeType.SelectOutput);
        NodeDefinition resultOutput = NodeDefinitionRegistry.Get(NodeType.ResultOutput);

        Assert.Contains(selectOutput.Pins, p => p.Direction == PinDirection.Input && p.Name == "order_by" && p.DataType == PinDataType.ColumnRef && p.AllowMultiple);
        Assert.Contains(selectOutput.Pins, p => p.Direction == PinDirection.Input && p.Name == "order_by_desc" && p.DataType == PinDataType.ColumnRef && p.AllowMultiple);
        Assert.Contains(selectOutput.Pins, p => p.Direction == PinDirection.Input && p.Name == "group_by" && p.DataType == PinDataType.ColumnRef && p.AllowMultiple);

        Assert.Contains(resultOutput.Pins, p => p.Direction == PinDirection.Input && p.Name == "order_by" && p.DataType == PinDataType.ColumnRef && p.AllowMultiple);
        Assert.Contains(resultOutput.Pins, p => p.Direction == PinDirection.Input && p.Name == "order_by_desc" && p.DataType == PinDataType.ColumnRef && p.AllowMultiple);
        Assert.Contains(resultOutput.Pins, p => p.Direction == PinDirection.Input && p.Name == "group_by" && p.DataType == PinDataType.ColumnRef && p.AllowMultiple);
    }

    [Fact]
    public void Compile_UsesResultOutputWires_ForSelectWhereHavingAndQualify_WhenLegacyBindingsAreEmpty()
    {
        NodeGraph graph = BuildGraphWithOutputSinkAndWires();

        CompiledNodeGraph compiled = new NodeGraphCompiler(graph, CreatePostgresContext()).Compile();

        Assert.Single(compiled.SelectExprs);
        Assert.Single(compiled.WhereExprs);
        Assert.Single(compiled.HavingExprs);
        Assert.Single(compiled.QualifyExprs);
    }

    [Fact]
    public void Compile_FallsBackToLegacyBindings_WhenOutputSinkWiresAreMissing()
    {
        NodeInstance table = CreateTableSource("tbl");
        NodeGraph graph = new()
        {
            Nodes = [table],
            SelectOutputs = [new SelectBinding("tbl", "id")],
            WhereConditions = [],
        };

        CompiledNodeGraph compiled = new NodeGraphCompiler(graph, CreatePostgresContext()).Compile();

        Assert.Single(compiled.SelectExprs);
    }

    [Fact]
    public void Compile_PrefersOutputSinkWhereWire_OverLegacyWhereBinding_WhenBothExist()
    {
        NodeGraph baseGraph = BuildGraphWithOutputSinkAndWires();
        NodeGraph graph = new()
        {
            Nodes = baseGraph.Nodes,
            Connections = baseGraph.Connections,
            WhereConditions = [new WhereBinding("eq_legacy", "result")],
        };

        CompiledNodeGraph compiled = new NodeGraphCompiler(graph, CreatePostgresContext()).Compile();
        string whereSql = Assert.Single(compiled.WhereExprs).Emit(CreatePostgresContext());

        Assert.Contains("\"id\"", whereSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"status\"", whereSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_PrefersOutputSinkOrderAndGroupWires_OverLegacyBindings_WhenBothExist()
    {
        NodeGraph graph = BuildGraphWithOutputSinkAndWires();
        graph = new NodeGraph
        {
            Nodes = graph.Nodes,
            Connections =
            [
                .. graph.Connections,
                new Connection("tbl", "id", "out", "order_by"),
                new Connection("tbl", "status", "out", "order_by_desc"),
                new Connection("tbl", "id", "out", "group_by"),
            ],
            OrderBys = [new OrderBinding("tbl", "legacy_col", Descending: true)],
            GroupBys = [new GroupByBinding("tbl", "legacy_group")],
        };

        CompiledNodeGraph compiled = new NodeGraphCompiler(graph, CreatePostgresContext()).Compile();

        Assert.Equal(2, compiled.OrderExprs.Count);
        Assert.Single(compiled.GroupByExprs);
        Assert.Contains("\"id\"", compiled.OrderExprs[0].Expr.Emit(CreatePostgresContext()), StringComparison.OrdinalIgnoreCase);
        Assert.True(compiled.OrderExprs[1].Desc);
        Assert.Contains("\"status\"", compiled.OrderExprs[1].Expr.Emit(CreatePostgresContext()), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"id\"", compiled.GroupByExprs[0].Emit(CreatePostgresContext()), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_MultipleTopLevelOutputSinks_ThrowsInvalidOperationException()
    {
        NodeInstance table = CreateTableSource("tbl");
        NodeInstance resultOutput = CreateOutput("out_1");
        NodeInstance selectOutput = new(
            "out_2",
            NodeType.SelectOutput,
            new Dictionary<string, string>(),
            new Dictionary<string, string>());

        NodeGraph graph = new()
        {
            Nodes = [table, resultOutput, selectOutput],
            Connections =
            [
                new Connection("tbl", "id", "out_1", "column"),
                new Connection("tbl", "id", "out_2", "column"),
            ],
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new NodeGraphCompiler(graph, CreatePostgresContext()).Compile());

        Assert.Contains("Multiple ResultOutput/SelectOutput", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_OutputSinkWithoutProjectionAndWithoutLegacySelect_ThrowsInvalidOperationException()
    {
        NodeInstance output = CreateOutput("out");
        NodeGraph graph = new()
        {
            Nodes = [output],
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new NodeGraphCompiler(graph, CreatePostgresContext()).Compile());

        Assert.Contains("no connected projection source", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static NodeGraph BuildGraphWithOutputSinkAndWires()
    {
        NodeInstance table = CreateTableSource("tbl");
        NodeInstance output = new(
            "out",
            NodeType.ResultOutput,
            new Dictionary<string, string>(),
            new Dictionary<string, string>());

        NodeInstance eqWhere = new(
            "eq_where",
            NodeType.Equals,
            new Dictionary<string, string> { ["right"] = "1" },
            new Dictionary<string, string>());

        NodeInstance eqQualify = new(
            "eq_qualify",
            NodeType.Equals,
            new Dictionary<string, string> { ["right"] = "READY" },
            new Dictionary<string, string>());

        NodeInstance eqLegacy = new(
            "eq_legacy",
            NodeType.Equals,
            new Dictionary<string, string> { ["right"] = "COMPLETED" },
            new Dictionary<string, string>());

        return new NodeGraph
        {
            Nodes = [table, output, eqWhere, eqQualify, eqLegacy],
            Connections =
            [
                new Connection("tbl", "id", "out", "column"),
                new Connection("tbl", "id", "eq_where", "left"),
                new Connection("tbl", "status", "eq_qualify", "left"),
                new Connection("tbl", "status", "eq_legacy", "left"),
                new Connection("eq_where", "result", "out", "where"),
                new Connection("eq_where", "result", "out", "having"),
                new Connection("eq_qualify", "result", "out", "qualify"),
            ],
        };
    }

    private static NodeInstance CreateTableSource(string id)
    {
        return new NodeInstance(
            id,
            NodeType.TableSource,
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            Alias: null,
            TableFullName: "public.orders",
            ColumnPins: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = "id",
                ["status"] = "status",
            },
            ColumnPinTypes: new Dictionary<string, PinDataType>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = PinDataType.Integer,
                ["status"] = PinDataType.Text,
            });
    }

    private static NodeInstance CreateOutput(string id)
    {
        return new NodeInstance(
            id,
            NodeType.ResultOutput,
            new Dictionary<string, string>(),
            new Dictionary<string, string>());
    }

    private static EmitContext CreatePostgresContext()
    {
        return new EmitContext(DatabaseProvider.Postgres, new SqlFunctionRegistry(DatabaseProvider.Postgres));
    }
}
