using System.Text.Json;
using DBWeaver.UI.Serialization;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas.Strategies;
using Xunit;

namespace DBWeaver.Tests.Unit.Serialization;

public class CanvasSerializerProviderConsistencyTests
{
    [Fact]
    public void SerializeWorkspace_DoesNotPersistDatabaseProvider()
    {
        var queryVm = new CanvasViewModel(null, null, null, null, new QueryDomainStrategy());
        var ddlVm = new CanvasViewModel(null, null, null, null, new DdlDomainStrategy());
        ddlVm.Provider = DBWeaver.Core.DatabaseProvider.SQLite;

        string json = CanvasSerializer.SerializeWorkspace(
            queryVm,
            ddlVm,
            provider: "Postgres",
            connectionName: "test");

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement queryCanvas = doc.RootElement.GetProperty("QueryCanvas");
        JsonElement ddlCanvas = doc.RootElement.GetProperty("DdlCanvas");

        Assert.False(queryCanvas.TryGetProperty("DatabaseProvider", out _));
        Assert.False(ddlCanvas.TryGetProperty("DatabaseProvider", out _));
    }

    [Fact]
    public void SerializeWorkspace_DoesNotPersistConnectionName()
    {
        var queryVm = new CanvasViewModel(null, null, null, null, new QueryDomainStrategy());
        var ddlVm = new CanvasViewModel(null, null, null, null, new DdlDomainStrategy());

        string json = CanvasSerializer.SerializeWorkspace(
            queryVm,
            ddlVm,
            provider: "Postgres",
            connectionName: "producao-interna");

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement queryCanvas = doc.RootElement.GetProperty("QueryCanvas");
        JsonElement ddlCanvas = doc.RootElement.GetProperty("DdlCanvas");

        Assert.False(queryCanvas.TryGetProperty("ConnectionName", out _));
        Assert.False(ddlCanvas.TryGetProperty("ConnectionName", out _));
    }
}
