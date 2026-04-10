using System.Text.Json;
using DBWeaver.UI.Services.Workspace.Models;

namespace DBWeaver.Tests.Unit.Workspace;

public class WorkspaceDocumentDescriptorTests
{
    [Fact]
    public void Descriptor_PreservesIdentityTypeAndPayloadContract()
    {
        Guid documentId = Guid.NewGuid();
        JsonElement payload = JsonSerializer.SerializeToElement(new { nodeCount = 3 });
        WorkspaceDocumentDescriptor descriptor = new(
            DocumentId: documentId,
            DocumentType: WorkspaceDocumentType.QueryCanvas,
            Title: "Query Canvas",
            IsDirty: true,
            PersistenceSchemaVersion: "1.0",
            Payload: payload);

        Assert.Equal(documentId, descriptor.DocumentId);
        Assert.Equal(WorkspaceDocumentType.QueryCanvas, descriptor.DocumentType);
        Assert.Equal("Query Canvas", descriptor.Title);
        Assert.True(descriptor.IsDirty);
        Assert.Equal("1.0", descriptor.PersistenceSchemaVersion);
        Assert.Equal(3, descriptor.Payload.GetProperty("nodeCount").GetInt32());
    }
}
