using DBWeaver.UI.Services.Workspace.Models;

namespace DBWeaver.UI.Services.Workspace.Pages;

public interface IWorkspaceDocumentPageContractRegistry
{
    WorkspaceDocumentPageContract Resolve(WorkspaceDocumentType documentType);
}
