using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainPlanExportFormatterTests
{
    [Fact]
    public void Format_IncludesProviderSqlAndPlanRows()
    {
        var sut = new ExplainPlanExportFormatter();
        var data = new ExplainPlanExportData(
            ProviderLabel: "PostgreSQL",
            Sql: "SELECT * FROM orders",
            Steps:
            [
                new ExplainStep
                {
                    Operation = "Seq Scan",
                    Detail = "relation=orders",
                    EstimatedCost = 98.4,
                    EstimatedRows = 1000,
                    ActualRows = 900,
                    IndentLevel = 0,
                    AlertLabel = "SEQ SCAN",
                },
            ],
            PlanningTimeMs: 0.123,
            ExecutionTimeMs: 1.456,
            GeneratedAtUtc: new DateTimeOffset(2026, 4, 3, 12, 0, 0, TimeSpan.Zero)
        );

        string text = sut.Format(data);

        Assert.Contains("EXPLAIN PLAN", text);
        Assert.Contains("Provider: PostgreSQL", text);
        Assert.Contains("SELECT * FROM orders", text);
        Assert.Contains("Planning (ms): 0.123", text);
        Assert.Contains("Execution (ms): 1.456", text);
        Assert.Contains("- Seq Scan | cost=98.4 | rows=1000 | actualRows=900 | alert=SEQ SCAN | relation=orders", text);
    }

    [Fact]
    public void Format_HandlesEmptySteps_WithNoStepsMarker()
    {
        var sut = new ExplainPlanExportFormatter();
        var data = new ExplainPlanExportData(
            ProviderLabel: "SQLite",
            Sql: "SELECT 1",
            Steps: [],
            PlanningTimeMs: null,
            ExecutionTimeMs: null,
            GeneratedAtUtc: DateTimeOffset.UtcNow
        );

        string text = sut.Format(data);

        Assert.Contains("Plan:", text);
        Assert.Contains("- (no steps)", text);
    }
}


