using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class MySqlExplainPlanParserTests
{
    [Fact]
    public void ParseJson_ReturnsEmpty_WhenInputIsBlank()
    {
        var sut = new MySqlExplainPlanParser();
        MySqlParsedPlan parsed = sut.ParseJson("  ");
        Assert.Empty(parsed.Nodes);
    }

    [Fact]
    public void ParseAnalyze_ReturnsEmpty_WhenInputIsBlank()
    {
        var sut = new MySqlExplainPlanParser();
        MySqlParsedPlan parsed = sut.ParseAnalyze(string.Empty);
        Assert.Empty(parsed.Nodes);
    }

    [Fact]
    public void ParseAnalyze_ParsesRowsCostsAndActuals()
    {
        const string analyze = """
            -> Limit: 10 row(s)  (cost=847.23 rows=10) (actual time=3.456..3.489 rows=10 loops=1)
                -> Table scan on orders  (cost=102.00 rows=1000) (actual time=0.041..2.143 rows=1000 loops=1)
            """;

        var sut = new MySqlExplainPlanParser();
        MySqlParsedPlan parsed = sut.ParseAnalyze(analyze);

        Assert.Equal(2, parsed.Nodes.Count);
        Assert.Equal(0, parsed.Nodes[0].IndentLevel);
        Assert.Equal(2, parsed.Nodes[1].IndentLevel);
        Assert.Equal(847.23, parsed.Nodes[0].EstimatedCost);
        Assert.Equal(10, parsed.Nodes[0].EstimatedRows);
        Assert.Equal(1000, parsed.Nodes[1].ActualRows);
        Assert.Equal("SEQ SCAN", parsed.Nodes[1].AlertLabel);
        Assert.Equal(3.489, parsed.ExecutionTimeMs);
    }

    [Fact]
    public void ParseJson_ParsesFilesortAndTableAccess()
    {
        const string json = """
            {
              "query_block": {
                "ordering_operation": {
                  "using_filesort": true,
                  "nested_loop": [{
                    "table": {
                      "table_name": "orders",
                      "access_type": "ALL",
                      "rows_examined_per_scan": 1000,
                      "cost_info": { "read_cost": "100.00", "eval_cost": "10.00" }
                    }
                  }]
                }
              }
            }
            """;

        var sut = new MySqlExplainPlanParser();
        MySqlParsedPlan parsed = sut.ParseJson(json);

        Assert.Contains(parsed.Nodes, n => n.NodeType == "Sort" && n.AlertLabel == "SORT");
        ExplainNode table = Assert.Single(parsed.Nodes, n => n.NodeType == "Table Access");
        Assert.Equal("orders (ALL)", table.Detail);
        Assert.Equal(110.0, table.EstimatedCost);
        Assert.Equal(1000, table.EstimatedRows);
        Assert.Equal("SEQ SCAN", table.AlertLabel);
    }
}


