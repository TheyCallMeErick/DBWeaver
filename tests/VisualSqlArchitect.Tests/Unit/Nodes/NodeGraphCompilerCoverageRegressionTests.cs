using Xunit;

namespace DBWeaver.Tests.Unit.Nodes;

public class NodeGraphCompilerCoverageRegressionTests
{
    [Fact]
    public void Ctor_NullGraph_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new NodeGraphCompiler(null!, NodeFixtures.Postgres));
    }

    [Fact]
    public void Ctor_NullContext_ThrowsArgumentNullException()
    {
        var graph = new NodeGraph();
        Assert.Throws<ArgumentNullException>(() => new NodeGraphCompiler(graph, null!));
    }

    [Fact]
    public void Compile_GraphCycle_ThrowsInvalidOperationException()
    {
        var n1 = new NodeInstance(
            "v1",
            NodeType.ValueString,
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["value"] = "A" }
        );
        var n2 = new NodeInstance(
            "v2",
            NodeType.ValueString,
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["value"] = "B" }
        );

        var graph = new NodeGraph
        {
            Nodes = [n1, n2],
            Connections =
            [
                new Connection("v1", "result", "v2", "input"),
                new Connection("v2", "result", "v1", "input"),
            ],
            SelectOutputs = [new SelectBinding("v1", "result")],
        };

        var compiler = new NodeGraphCompiler(graph, NodeFixtures.Postgres);
        Assert.Throws<InvalidOperationException>(() => compiler.Compile());
    }

    [Fact]
    public void Compile_TableSourceWithoutTableFullName_ThrowsInvalidOperationException()
    {
        var table = new NodeInstance(
            "tbl",
            NodeType.TableSource,
            new Dictionary<string, string>(),
            new Dictionary<string, string>()
        );

        var graph = new NodeGraph
        {
            Nodes = [table],
            SelectOutputs = [new SelectBinding("tbl", "id")],
        };

        var compiler = new NodeGraphCompiler(graph, NodeFixtures.Postgres);
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => compiler.Compile());
        Assert.Contains("has no TableFullName", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_PopulatesWhereHavingQualifyOrderAndGroupCollections()
    {
        var table = new NodeInstance(
            "tbl",
            NodeType.TableSource,
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            TableFullName: "public.orders"
        );

        var gtTotal = new NodeInstance(
            "gt_total",
            NodeType.GreaterThan,
            new Dictionary<string, string> { ["right"] = "100" },
            new Dictionary<string, string>()
        );

        var eqStatus = new NodeInstance(
            "eq_status",
            NodeType.Equals,
            new Dictionary<string, string> { ["right"] = "COMPLETED" },
            new Dictionary<string, string>()
        );

        var graph = new NodeGraph
        {
            Nodes = [table, gtTotal, eqStatus],
            Connections =
            [
                new Connection("tbl", "total", "gt_total", "left"),
                new Connection("tbl", "status", "eq_status", "left"),
            ],
            SelectOutputs = [new SelectBinding("tbl", "status")],
            WhereConditions =
            [
                new WhereBinding("gt_total", "result"),
                new WhereBinding("eq_status", "result"),
            ],
            Havings = [new HavingBinding("gt_total", "result")],
            Qualifies = [new QualifyBinding("eq_status", "result")],
            OrderBys = [new OrderBinding("tbl", "created_at", Descending: true)],
            GroupBys = [new GroupByBinding("tbl", "status")],
        };

        CompiledNodeGraph compiled = new NodeGraphCompiler(graph, NodeFixtures.Postgres).Compile();

        Assert.Equal(2, compiled.WhereExprs.Count);
        Assert.Single(compiled.HavingExprs);
        Assert.Single(compiled.QualifyExprs);
        Assert.Single(compiled.OrderExprs);
        Assert.Single(compiled.GroupByExprs);
    }
}
