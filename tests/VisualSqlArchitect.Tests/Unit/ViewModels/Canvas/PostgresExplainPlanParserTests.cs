using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class PostgresExplainPlanParserTests
{
    [Fact]
    public void Parse_MapsPlanTreeAndTimes_FromValidJson()
    {
        const string json = """
            [{
              "Plan": {
                "Node Type": "Limit",
                "Total Cost": 203.70,
                "Plan Rows": 100,
                "Plans": [{
                  "Node Type": "Sort",
                  "Total Cost": 208.45,
                  "Plan Rows": 2000,
                  "Plans": [{
                    "Node Type": "Hash Join",
                    "Total Cost": 178.20,
                    "Plan Rows": 2000,
                    "Hash Cond": "(oi.order_id = o.id)",
                    "Plans": [{
                      "Node Type": "Seq Scan",
                      "Relation Name": "order_items",
                      "Total Cost": 98.00,
                      "Plan Rows": 4800,
                      "Actual Rows": 48231
                    }]
                  }]
                }]
              },
              "Planning Time": 0.234,
              "Execution Time": 1.567
            }]
            """;

        var sut = new PostgresExplainPlanParser();
        PostgresParsedPlan result = sut.Parse(json);

        Assert.Equal(4, result.Nodes.Count);
        Assert.Equal("Limit", result.Nodes[0].NodeType);
        Assert.Equal("Sort", result.Nodes[1].NodeType);
        Assert.Equal("SORT", result.Nodes[1].AlertLabel);
        Assert.Equal("Hash Join", result.Nodes[2].NodeType);
        Assert.Equal("HASH", result.Nodes[2].AlertLabel);
        Assert.Equal("Seq Scan", result.Nodes[3].NodeType);
        Assert.Equal("SEQ SCAN", result.Nodes[3].AlertLabel);
        Assert.Equal(48231, result.Nodes[3].ActualRows);
        Assert.Equal(0.234, result.PlanningTimeMs);
        Assert.Equal(1.567, result.ExecutionTimeMs);
        Assert.Equal(3, result.Nodes[3].IndentLevel);
    }

    [Fact]
    public void Parse_Throws_WhenPlanIsMissing()
    {
        const string invalid = """[{"Execution Time": 1.0}]""";
        var sut = new PostgresExplainPlanParser();

        Assert.Throws<InvalidOperationException>(() => sut.Parse(invalid));
    }
}


