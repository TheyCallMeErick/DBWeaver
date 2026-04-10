using System.Text.Json;

namespace DBWeaver.UI.Services.Workspace.Models;

public sealed record WorkspaceDocumentDescriptor(
    Guid DocumentId,
    WorkspaceDocumentType DocumentType,
    string Title,
    bool IsDirty,
    string PersistenceSchemaVersion,
    JsonElement Payload);
