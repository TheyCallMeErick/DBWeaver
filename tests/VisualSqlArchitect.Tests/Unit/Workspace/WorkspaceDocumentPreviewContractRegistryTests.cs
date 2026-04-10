using DBWeaver.UI.Services.Workspace.Models;
using DBWeaver.UI.Services.Workspace.Preview;

namespace DBWeaver.Tests.Unit.Workspace;

public class WorkspaceDocumentPreviewContractRegistryTests
{
    [Theory]
    [InlineData(WorkspaceDocumentType.QueryCanvas, WorkspaceDocumentPreviewKind.Query)]
    [InlineData(WorkspaceDocumentType.DdlCanvas, WorkspaceDocumentPreviewKind.Ddl)]
    [InlineData(WorkspaceDocumentType.SqlEditor, WorkspaceDocumentPreviewKind.Unavailable)]
    public void Resolve_ReturnsExpectedPreviewKindForDocumentType(
        WorkspaceDocumentType documentType,
        WorkspaceDocumentPreviewKind expectedKind)
    {
        var registry = new WorkspaceDocumentPreviewContractRegistry();

        WorkspaceDocumentPreviewContract contract = registry.Resolve(documentType);

        Assert.Equal(expectedKind, contract.Kind);
        Assert.False(string.IsNullOrWhiteSpace(contract.Title));
        Assert.False(string.IsNullOrWhiteSpace(contract.PrimaryTabLabel));
        Assert.False(string.IsNullOrWhiteSpace(contract.UnavailableMessage));
    }
}
