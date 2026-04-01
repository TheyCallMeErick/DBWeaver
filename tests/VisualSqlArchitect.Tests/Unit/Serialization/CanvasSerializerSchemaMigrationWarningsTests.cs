using VisualSqlArchitect.UI.Serialization;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Serialization;

public class CanvasSerializerSchemaMigrationWarningsTests
{
    [Fact]
    public void Deserialize_V1File_EmitsStepWarningsAndSummary()
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

        Assert.True(result.Success);
        Assert.NotNull(result.Warnings);
        Assert.Contains(result.Warnings!, w => w.Contains("v1 -> v2", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Warnings!, w => w.Contains("v2 -> v3", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Warnings!, w => w.Contains("summary", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Deserialize_V2File_EmitsUpgradeToV3Warning()
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

        Assert.True(result.Success);
        Assert.NotNull(result.Warnings);
        Assert.Contains(result.Warnings!, w => w.Contains("v2 -> v3", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Warnings!, w => w.Contains("schema v2", StringComparison.OrdinalIgnoreCase) || w.Contains("summary", StringComparison.OrdinalIgnoreCase));
    }
}
