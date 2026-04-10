namespace DBWeaver.UI.Services.Workspace.Preview;

public sealed record WorkspaceDocumentPreviewContract(
    WorkspaceDocumentPreviewKind Kind,
    string Title,
    string PrimaryTabLabel,
    string UnavailableMessage);
