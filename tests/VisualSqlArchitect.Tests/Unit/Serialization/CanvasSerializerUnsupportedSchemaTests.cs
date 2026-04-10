using DBWeaver.UI.Serialization;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.Serialization;

public class CanvasSerializerUnsupportedSchemaTests
{
    [Fact]
    public void Deserialize_V1File_FailsAsUnsupported()
    {
        string v1Json =
            """
            {
              "Version": 1,
              "DatabaseProvider": "Postgres",
              "ConnectionName": "legacy-v1",
              "Zoom": 1.0,
              "PanX": 0.0,
              "PanY": 0.0,
              "Nodes": [],
              "Connections": [],
              "SelectBindings": [],
              "WhereBindings": []
            }
            """;

        var vm = new CanvasViewModel();
        vm.Nodes.Clear();
        vm.Connections.Clear();

        CanvasLoadResult result = CanvasSerializer.Deserialize(v1Json, vm);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("Unsupported canvas schema version", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_V2File_FailsAsUnsupported()
    {
        string v2Json =
            """
            {
              "Version": 2,
              "DatabaseProvider": "Postgres",
              "ConnectionName": "legacy-v2",
              "Zoom": 1.0,
              "PanX": 0.0,
              "PanY": 0.0,
              "Nodes": [],
              "Connections": [],
              "SelectBindings": [],
              "WhereBindings": []
            }
            """;

        var vm = new CanvasViewModel();
        vm.Nodes.Clear();
        vm.Connections.Clear();

        CanvasLoadResult result = CanvasSerializer.Deserialize(v2Json, vm);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("Unsupported canvas schema version", result.Error!, StringComparison.OrdinalIgnoreCase);
    }
}
