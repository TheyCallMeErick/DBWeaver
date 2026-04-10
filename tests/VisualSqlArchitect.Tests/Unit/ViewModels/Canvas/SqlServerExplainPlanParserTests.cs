using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class SqlServerExplainPlanParserTests
{
    [Fact]
    public void Parse_ReturnsEmpty_WhenXmlBlank()
    {
        var sut = new SqlServerExplainPlanParser();
        SqlServerParsedPlan parsed = sut.Parse(" ");
        Assert.Empty(parsed.Nodes);
    }

    [Fact]
    public void Parse_MapsRelOpHierarchyAndAlerts()
    {
        const string xml = """
            <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan">
              <BatchSequence><Batch><Statements><StmtSimple>
                <QueryPlan>
                  <RelOp PhysicalOp="Sort" EstimateRows="2" EstimatedTotalSubtreeCost="0.2">
                    <RelOp PhysicalOp="Table Scan" EstimateRows="10" EstimatedTotalSubtreeCost="1.5">
                      <IndexScan>
                        <Object Table="[dbo].[orders]" Index="[IX_orders_status]" />
                      </IndexScan>
                    </RelOp>
                  </RelOp>
                </QueryPlan>
              </StmtSimple></Statements></Batch></BatchSequence>
            </ShowPlanXML>
            """;

        var sut = new SqlServerExplainPlanParser();
        SqlServerParsedPlan parsed = sut.Parse(xml);

        Assert.Equal(2, parsed.Nodes.Count);
        ExplainNode sort = parsed.Nodes[0];
        ExplainNode scan = parsed.Nodes[1];

        Assert.Equal("Sort", sort.NodeType);
        Assert.Equal("SORT", sort.AlertLabel);
        Assert.Equal(0, sort.IndentLevel);
        Assert.Equal(1, scan.IndentLevel);
        Assert.Equal("Table Scan", scan.NodeType);
        Assert.Equal("SEQ SCAN", scan.AlertLabel);
        Assert.Contains("orders", scan.Detail!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(10, scan.EstimatedRows);
        Assert.Equal(1.5, scan.EstimatedCost);
    }
}


