using DBWeaver.UI.Services.Workspace.Models;

namespace DBWeaver.UI.Services.Workspace.Diagnostics;

public interface IWorkspaceDocumentDiagnosticsContractRegistry
{
    WorkspaceDocumentDiagnosticsContract Resolve(WorkspaceDocumentType documentType);
}
