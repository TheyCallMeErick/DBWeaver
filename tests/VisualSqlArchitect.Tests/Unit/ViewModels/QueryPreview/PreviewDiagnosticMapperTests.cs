using DBWeaver.UI.Services.QueryPreview.Models;
using DBWeaver.UI.Services.QueryPreview;

namespace DBWeaver.Tests.Unit.ViewModels.QueryPreview;

public class PreviewDiagnosticMapperTests
{
    [Fact]
    public void PreviewDiagnostic_StoresAllProperties()
    {
        var diagnostic = new PreviewDiagnostic(
            PreviewDiagnosticSeverity.Warning,
            PreviewDiagnosticCategory.Predicate,
            "W-PRD-001",
            "Predicate missing input",
            nodeId: "node-1",
            pinName: "left");

        Assert.Equal(PreviewDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(PreviewDiagnosticCategory.Predicate, diagnostic.Category);
        Assert.Equal("W-PRD-001", diagnostic.Code);
        Assert.Equal("Predicate missing input", diagnostic.Message);
        Assert.Equal("node-1", diagnostic.NodeId);
        Assert.Equal("left", diagnostic.PinName);
    }

    [Fact]
    public void FromLegacyMessage_CteCycle_AssignsCteErrorCode()
    {
        const string message = "Cycle detected between CTE definitions: cte_orders";

        PreviewDiagnostic diagnostic = PreviewDiagnosticMapper.FromLegacyMessage(message);

        Assert.Equal(PreviewDiagnosticCategory.Cte, diagnostic.Category);
        Assert.Equal(PreviewDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("E-CTE-001", diagnostic.Code);
        Assert.Equal(message, diagnostic.Message);
    }

    [Fact]
    public void FromLegacyMessage_TypeMismatch_AssignsTypeCompatibilityCode()
    {
        const string message = "Pin type mismatch detected between Number and String.";

        PreviewDiagnostic diagnostic = PreviewDiagnosticMapper.FromLegacyMessage(message);

        Assert.Equal(PreviewDiagnosticCategory.TypeCompatibility, diagnostic.Category);
        Assert.Equal(PreviewDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("W-TYP-001", diagnostic.Code);
    }

    [Fact]
    public void FromLegacyMessage_WindowMissingRequired_UpgradesToErrorCode()
    {
        const string message = "Window function is missing required 'value' input.";

        PreviewDiagnostic diagnostic = PreviewDiagnosticMapper.FromLegacyMessage(message);

        Assert.Equal(PreviewDiagnosticCategory.Window, diagnostic.Category);
        Assert.Equal(PreviewDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("E-WIN-001", diagnostic.Code);
    }

    [Fact]
    public void FromLegacyMessage_InfoPrefix_AssignsInfoSeverity()
    {
        const string message = "info: this is only an informational hint";

        PreviewDiagnostic diagnostic = PreviewDiagnosticMapper.FromLegacyMessage(message);

        Assert.Equal(PreviewDiagnosticCategory.General, diagnostic.Category);
        Assert.Equal(PreviewDiagnosticSeverity.Info, diagnostic.Severity);
        Assert.Equal("W-GEN-001", diagnostic.Code);
    }

    [Fact]
    public void FromLegacyMessages_IgnoresEmptyEntries()
    {
        List<PreviewDiagnostic> diagnostics = PreviewDiagnosticMapper.FromLegacyMessages(
        [
            "",
            " ",
            "invalid hint",
        ]);

        Assert.Single(diagnostics);
        Assert.Equal("E-GEN-001", diagnostics[0].Code);
    }
}


