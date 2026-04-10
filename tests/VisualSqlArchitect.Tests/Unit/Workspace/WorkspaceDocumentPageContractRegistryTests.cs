using DBWeaver.UI.Services.Workspace.Models;
using DBWeaver.UI.Services.Workspace.Pages;

namespace DBWeaver.Tests.Unit.Workspace;

public class WorkspaceDocumentPageContractRegistryTests
{
    [Theory]
    [InlineData(WorkspaceDocumentType.QueryCanvas, true, false, false, true, false, false, true)]
    [InlineData(WorkspaceDocumentType.DdlCanvas, false, true, false, true, false, false, true)]
    [InlineData(WorkspaceDocumentType.SqlEditor, false, false, true, false, true, false, false)]
    public void Resolve_ReturnsExpectedContractByDocumentType(
        WorkspaceDocumentType documentType,
        bool showsQueryCanvasPage,
        bool showsDdlCanvasPage,
        bool showsSqlEditorPage,
        bool showsDiagramSidebar,
        bool showsSqlEditorSidebar,
        bool showsQueryTabs,
        bool canCollapseSidebars)
    {
        var registry = new WorkspaceDocumentPageContractRegistry();

        WorkspaceDocumentPageContract contract = registry.Resolve(documentType);

        Assert.Equal(showsQueryCanvasPage, contract.ShowsQueryCanvasPage);
        Assert.Equal(showsDdlCanvasPage, contract.ShowsDdlCanvasPage);
        Assert.Equal(showsSqlEditorPage, contract.ShowsSqlEditorPage);
        Assert.Equal(showsDiagramSidebar, contract.ShowsDiagramSidebar);
        Assert.Equal(showsSqlEditorSidebar, contract.ShowsSqlEditorSidebar);
        Assert.Equal(showsQueryTabs, contract.ShowsQueryTabs);
        Assert.Equal(canCollapseSidebars, contract.CanCollapseSidebars);
    }
}
