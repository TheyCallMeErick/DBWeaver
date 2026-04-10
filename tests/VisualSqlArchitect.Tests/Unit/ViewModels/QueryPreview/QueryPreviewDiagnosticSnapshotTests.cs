using DBWeaver.UI.Services.QueryPreview;

namespace DBWeaver.Tests.Unit.ViewModels.QueryPreview;

public class QueryPreviewDiagnosticSnapshotTests
{
    [Fact]
    public void PreviewDiagnosticCodes_MatchCriticalSnapshot()
    {
        string[] criticalMessages =
        [
            "Cycle detected between CTE definitions",
            "Window function is missing required 'value' input.",
            "COMPILE WHERE node connected to WHERE/HAVING/QUALIFY has no conditions",
            "Incompatible connection: A (Number) -> B (Text).",
            "Join node is incomplete",
            "Subquery source alias is required. Defaulting alias to 'subq'.",
            "LIKE node connected to WHERE/HAVING/QUALIFY has empty pattern parameter.",
            "Some neutral warning not mapped",
        ];

        string[] actualCodes = PreviewDiagnosticMapper
            .FromLegacyMessages(criticalMessages)
            .Select(d => d.Code)
            .ToArray();

        string snapshotPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Snapshots", "querypreview-diagnostic-codes.snap");
        string[] expectedCodes = File.ReadAllLines(snapshotPath)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        Assert.Equal(expectedCodes, actualCodes);
    }
}

