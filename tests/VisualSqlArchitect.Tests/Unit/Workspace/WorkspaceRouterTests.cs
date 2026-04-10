using System.Text.Json;
using DBWeaver.UI.Services.Workspace;
using DBWeaver.UI.Services.Workspace.Models;

namespace DBWeaver.Tests.Unit.Workspace;

public class WorkspaceRouterTests
{
    [Fact]
    public void OpenDocument_RegistersDocumentAndSetsActiveDocument()
    {
        var router = new WorkspaceRouter();
        OpenWorkspaceDocument queryDocument = CreateDocument(WorkspaceDocumentType.QueryCanvas, "Query");

        router.OpenDocument(queryDocument);

        Assert.Single(router.OpenDocuments);
        Assert.Equal(queryDocument.Descriptor.DocumentId, router.ActiveDocumentId);
        Assert.Same(queryDocument, router.ActiveDocument);
    }

    [Fact]
    public void TryActivateByType_ActivatesMatchingDocument()
    {
        var router = new WorkspaceRouter();
        OpenWorkspaceDocument queryDocument = CreateDocument(WorkspaceDocumentType.QueryCanvas, "Query");
        OpenWorkspaceDocument ddlDocument = CreateDocument(WorkspaceDocumentType.DdlCanvas, "DDL");
        router.OpenDocument(queryDocument);
        router.OpenDocument(ddlDocument);

        bool changed = router.TryActivateByType(WorkspaceDocumentType.QueryCanvas);

        Assert.True(changed);
        Assert.Equal(queryDocument.Descriptor.DocumentId, router.ActiveDocumentId);
    }

    [Fact]
    public void TryClose_WhenClosingActiveDocument_SelectsAdjacentDocumentDeterministically()
    {
        var router = new WorkspaceRouter();
        OpenWorkspaceDocument queryDocument = CreateDocument(WorkspaceDocumentType.QueryCanvas, "Query");
        OpenWorkspaceDocument ddlDocument = CreateDocument(WorkspaceDocumentType.DdlCanvas, "DDL");
        OpenWorkspaceDocument sqlEditorDocument = CreateDocument(WorkspaceDocumentType.SqlEditor, "SQL");
        router.OpenDocument(queryDocument);
        router.OpenDocument(ddlDocument);
        router.OpenDocument(sqlEditorDocument);

        bool closed = router.TryClose(ddlDocument.Descriptor.DocumentId);

        Assert.True(closed);
        Assert.Equal(sqlEditorDocument.Descriptor.DocumentId, router.ActiveDocumentId);
        Assert.Equal(2, router.OpenDocuments.Count);
    }

    [Fact]
    public void TryActivate_WhenDocumentIdDoesNotExist_DoesNotChangeActiveDocument()
    {
        var router = new WorkspaceRouter();
        OpenWorkspaceDocument queryDocument = CreateDocument(WorkspaceDocumentType.QueryCanvas, "Query");
        OpenWorkspaceDocument ddlDocument = CreateDocument(WorkspaceDocumentType.DdlCanvas, "DDL");
        router.OpenDocument(queryDocument);
        router.OpenDocument(ddlDocument);
        Guid activeBefore = router.ActiveDocumentId ?? Guid.Empty;

        bool changed = router.TryActivate(Guid.NewGuid());

        Assert.False(changed);
        Assert.Equal(activeBefore, router.ActiveDocumentId);
    }

    private static OpenWorkspaceDocument CreateDocument(WorkspaceDocumentType type, string title)
    {
        WorkspaceDocumentDescriptor descriptor = new(
            DocumentId: Guid.NewGuid(),
            DocumentType: type,
            Title: title,
            IsDirty: false,
            PersistenceSchemaVersion: "1.0",
            Payload: JsonSerializer.SerializeToElement(new { }));

        return new OpenWorkspaceDocument(
            Descriptor: descriptor,
            DocumentViewModel: new object(),
            PageViewModel: null,
            PageState: null);
    }
}
