using DBWeaver.UI.Services.Workspace.Models;

namespace DBWeaver.UI.Services.Workspace.Pages;

public sealed class WorkspaceDocumentPageContractRegistry : IWorkspaceDocumentPageContractRegistry
{
    private static readonly WorkspaceDocumentPageContract QueryContract = new(
        ShowsQueryCanvasPage: true,
        ShowsDdlCanvasPage: false,
        ShowsSqlEditorPage: false,
        ShowsDiagramSidebar: true,
        ShowsSqlEditorSidebar: false,
        ShowsQueryTabs: false,
        CanCollapseSidebars: true);

    private static readonly WorkspaceDocumentPageContract DdlContract = new(
        ShowsQueryCanvasPage: false,
        ShowsDdlCanvasPage: true,
        ShowsSqlEditorPage: false,
        ShowsDiagramSidebar: true,
        ShowsSqlEditorSidebar: false,
        ShowsQueryTabs: false,
        CanCollapseSidebars: true);

    private static readonly WorkspaceDocumentPageContract SqlEditorContract = new(
        ShowsQueryCanvasPage: false,
        ShowsDdlCanvasPage: false,
        ShowsSqlEditorPage: true,
        ShowsDiagramSidebar: false,
        ShowsSqlEditorSidebar: true,
        ShowsQueryTabs: false,
        CanCollapseSidebars: false);

    public WorkspaceDocumentPageContract Resolve(WorkspaceDocumentType documentType)
    {
        return documentType switch
        {
            WorkspaceDocumentType.QueryCanvas => QueryContract,
            WorkspaceDocumentType.DdlCanvas => DdlContract,
            WorkspaceDocumentType.SqlEditor => SqlEditorContract,
            _ => QueryContract,
        };
    }
}
