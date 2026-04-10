namespace DBWeaver.UI.Services.Workspace.Models;

public sealed record OpenWorkspaceDocument(
    WorkspaceDocumentDescriptor Descriptor,
    object DocumentViewModel,
    object? PageViewModel,
    object? PageState);
