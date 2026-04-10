using Avalonia;
using System.Text.Json;
using DBWeaver.Nodes;
using DBWeaver.UI.Serialization;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.Serialization;

public class CanvasSerializerConnectionIntegrityTests
{
    [Fact]
    public void Serialize_DoesNotPersistHalfBuiltConnections()
    {
        var vm = new CanvasViewModel();
        vm.Nodes.Clear();
        vm.Connections.Clear();

        var fromNode = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        vm.Nodes.Add(fromNode);

        // Simulate drag-in-progress connection (ToPin not selected yet)
        var pending = new ConnectionViewModel(fromNode.OutputPins[0], default, default)
        {
            ToPin = null
        };
        vm.Connections.Add(pending);

        string json = CanvasSerializer.Serialize(vm);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement conns = doc.RootElement.GetProperty("Connections");
        Assert.Empty(conns.EnumerateArray());
    }

    [Fact]
    public void Deserialize_AddsWarningForMalformedConnection()
    {
        var vm = new CanvasViewModel();

        string malformedJson =
            """
            {
              "Version": 3,
              "DatabaseProvider": "Postgres",
              "ConnectionName": "test",
              "Zoom": 1.0,
              "PanX": 0.0,
              "PanY": 0.0,
              "Nodes": [],
              "Connections": [
                {
                  "FromNodeId": "n1",
                  "FromPinName": "id",
                  "ToNodeId": "",
                  "ToPinName": ""
                }
              ],
              "SelectBindings": [],
              "WhereBindings": []
            }
            """;

        CanvasLoadResult result = CanvasSerializer.Deserialize(malformedJson, vm);

        Assert.True(result.Success);
        Assert.NotNull(result.Warnings);
        Assert.Contains(result.Warnings!, w => w.Contains("malformed connection", StringComparison.OrdinalIgnoreCase));
    }
}
