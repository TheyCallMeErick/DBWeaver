namespace DBWeaver.UI.Services.Workspace.Pages;

public sealed record WorkspaceDocumentPageContract(
    bool ShowsQueryCanvasPage,
    bool ShowsDdlCanvasPage,
    bool ShowsSqlEditorPage,
    bool ShowsDiagramSidebar,
    bool ShowsSqlEditorSidebar,
    bool ShowsQueryTabs,
    bool CanCollapseSidebars);
