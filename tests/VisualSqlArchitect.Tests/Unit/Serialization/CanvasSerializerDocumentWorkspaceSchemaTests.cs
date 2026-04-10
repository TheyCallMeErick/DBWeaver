using System.Text.Json;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.Serialization;
using DBWeaver.UI.Services.Workspace.Models;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas.Strategies;

namespace DBWeaver.Tests.Unit.Serialization;

public class CanvasSerializerDocumentWorkspaceSchemaTests
{
    [Fact]
    public void SerializeWorkspace_WritesDocumentCollectionSchema()
    {
        using var queryVm = new CanvasViewModel();
        using var ddlVm = new CanvasViewModel(null, null, null, null, new DdlDomainStrategy());

        string json = CanvasSerializer.SerializeWorkspace(
            queryVm,
            ddlVm,
            activeDocumentType: WorkspaceDocumentType.SqlEditor);

        SavedWorkspaceDocumentsCanvas? saved = JsonSerializer.Deserialize<SavedWorkspaceDocumentsCanvas>(json);
        Assert.NotNull(saved);
        Assert.Equal(CanvasSerializer.CurrentSchemaVersion, saved!.Version);
        Assert.NotNull(saved.ActiveDocumentId);
        Assert.NotEmpty(saved.Documents);
        Assert.Contains(saved.Documents, document =>
            string.Equals(document.DocumentType, WorkspaceDocumentType.QueryCanvas.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(saved.Documents, document =>
            string.Equals(document.DocumentType, WorkspaceDocumentType.DdlCanvas.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(saved.Documents, document =>
            string.Equals(document.DocumentType, WorkspaceDocumentType.SqlEditor.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DeserializeWorkspace_LegacyV4_UsesDeterministicActiveDocumentPolicy()
    {
        using var queryVm = new CanvasViewModel();
        using var ddlVm = new CanvasViewModel(null, null, null, null, new DdlDomainStrategy());
        ddlVm.SpawnNode(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(40, 40));

        SavedCanvas queryCanvas = JsonSerializer.Deserialize<SavedCanvas>(CanvasSerializer.Serialize(queryVm))!;
        SavedCanvas ddlCanvas = JsonSerializer.Deserialize<SavedCanvas>(CanvasSerializer.Serialize(ddlVm))!;

        string legacyJson = JsonSerializer.Serialize(new SavedWorkspaceCanvas(
            Version: 4,
            QueryCanvas: queryCanvas,
            DdlCanvas: ddlCanvas));

        CanvasLoadResult result = CanvasSerializer.DeserializeWorkspace(legacyJson, queryVm, ddlVm);

        Assert.True(result.Success);
        Assert.Equal(WorkspaceDocumentType.DdlCanvas, result.ActiveDocumentType);
    }
}
