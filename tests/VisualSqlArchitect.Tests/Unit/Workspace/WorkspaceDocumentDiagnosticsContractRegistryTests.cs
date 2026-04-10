using DBWeaver.UI.Services.Workspace.Diagnostics;
using DBWeaver.UI.Services.Workspace.Models;

namespace DBWeaver.Tests.Unit.Workspace;

public class WorkspaceDocumentDiagnosticsContractRegistryTests
{
    [Theory]
    [InlineData(WorkspaceDocumentType.QueryCanvas, true)]
    [InlineData(WorkspaceDocumentType.DdlCanvas, true)]
    [InlineData(WorkspaceDocumentType.SqlEditor, false)]
    public void Resolve_ReturnsExpectedDiagnosticsAvailabilityForDocumentType(
        WorkspaceDocumentType documentType,
        bool hasLocalDiagnostics)
    {
        var registry = new WorkspaceDocumentDiagnosticsContractRegistry();

        WorkspaceDocumentDiagnosticsContract contract = registry.Resolve(documentType);

        Assert.Equal(hasLocalDiagnostics, contract.HasLocalDiagnostics);
    }
}
