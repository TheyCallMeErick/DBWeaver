using DBWeaver.UI.Services.Workspace.Models;

namespace DBWeaver.UI.Services.Workspace.Diagnostics;

public sealed class WorkspaceDocumentDiagnosticsContractRegistry : IWorkspaceDocumentDiagnosticsContractRegistry
{
    private static readonly WorkspaceDocumentDiagnosticsContract HasDiagnosticsContract = new(HasLocalDiagnostics: true);
    private static readonly WorkspaceDocumentDiagnosticsContract NoDiagnosticsContract = new(HasLocalDiagnostics: false);

    public WorkspaceDocumentDiagnosticsContract Resolve(WorkspaceDocumentType documentType)
    {
        return documentType switch
        {
            WorkspaceDocumentType.QueryCanvas => HasDiagnosticsContract,
            WorkspaceDocumentType.DdlCanvas => HasDiagnosticsContract,
            WorkspaceDocumentType.SqlEditor => NoDiagnosticsContract,
            _ => NoDiagnosticsContract,
        };
    }
}
