using DBWeaver.UI.Services.Workspace.Models;

namespace DBWeaver.UI.Services.Workspace;

public interface IWorkspaceRouter
{
    IReadOnlyList<OpenWorkspaceDocument> OpenDocuments { get; }

    Guid? ActiveDocumentId { get; }

    OpenWorkspaceDocument? ActiveDocument { get; }

    void OpenDocument(OpenWorkspaceDocument document, bool activate = true);

    bool TryActivate(Guid documentId);

    bool TryActivateByType(WorkspaceDocumentType documentType);

    bool TryClose(Guid documentId);

    void ReplaceDocuments(IReadOnlyList<OpenWorkspaceDocument> documents, Guid? activeDocumentId);
}
