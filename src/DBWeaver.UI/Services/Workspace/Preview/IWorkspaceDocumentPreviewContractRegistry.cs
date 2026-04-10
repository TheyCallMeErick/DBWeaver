using DBWeaver.UI.Services.Workspace.Models;

namespace DBWeaver.UI.Services.Workspace.Preview;

public interface IWorkspaceDocumentPreviewContractRegistry
{
    WorkspaceDocumentPreviewContract Resolve(WorkspaceDocumentType documentType);
}
