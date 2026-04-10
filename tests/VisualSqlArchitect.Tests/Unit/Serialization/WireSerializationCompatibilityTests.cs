using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.Serialization;
using DBWeaver.UI.ViewModels;
using System.Text.Json.Nodes;

namespace DBWeaver.Tests.Unit.Serialization;

public sealed class WireSerializationCompatibilityTests
{
    [Fact]
    public void SerializeThenDeserialize_PreservesRoutingModeAndBreakpoints()
    {
        using var sourceCanvas = CreateSimpleConnectedCanvas();
        ConnectionViewModel sourceWire = Assert.Single(sourceCanvas.Connections);
        sourceWire.RoutingMode = CanvasWireRoutingMode.Orthogonal;
        sourceWire.SetBreakpoints(
        [
            new WireBreakpoint(new Point(160, 80)),
            new WireBreakpoint(new Point(220, 80)),
        ]);

        string json = CanvasSerializer.Serialize(sourceCanvas);

        using var loadedCanvas = new CanvasViewModel();
        CanvasLoadResult load = CanvasSerializer.Deserialize(json, loadedCanvas);

        Assert.True(load.Success, load.Error);
        ConnectionViewModel restored = Assert.Single(loadedCanvas.Connections);
        Assert.Equal(CanvasWireRoutingMode.Orthogonal, restored.RoutingMode);
        Assert.Equal(2, restored.Breakpoints.Count);
        Assert.Equal(new Point(160, 80), restored.Breakpoints[0].Position);
        Assert.Equal(new Point(220, 80), restored.Breakpoints[1].Position);
    }

    [Fact]
    public void Deserialize_LegacyConnectionWithoutWireMetadata_DefaultsToBezierAndNoBreakpoints()
    {
        using var sourceCanvas = CreateSimpleConnectedCanvas();
        ConnectionViewModel sourceWire = Assert.Single(sourceCanvas.Connections);
        sourceWire.RoutingMode = CanvasWireRoutingMode.Orthogonal;
        sourceWire.SetBreakpoints([new WireBreakpoint(new Point(160, 80))]);

        string json = CanvasSerializer.Serialize(sourceCanvas);
        JsonObject root = JsonNode.Parse(json)!.AsObject();
        JsonObject legacyConnection = root["Connections"]!.AsArray()[0]!.AsObject();
        legacyConnection.Remove("RoutingMode");
        legacyConnection.Remove("Breakpoints");

        using var loadedCanvas = new CanvasViewModel();
        CanvasLoadResult load = CanvasSerializer.Deserialize(root.ToJsonString(), loadedCanvas);

        Assert.True(load.Success, load.Error);
        ConnectionViewModel restored = Assert.Single(loadedCanvas.Connections);
        Assert.Equal(CanvasWireRoutingMode.Bezier, restored.RoutingMode);
        Assert.Empty(restored.Breakpoints);
    }

    [Fact]
    public void SerializeThenDeserialize_PreservesInactiveBreakpointsOutsideOrthogonalMode()
    {
        using var sourceCanvas = CreateSimpleConnectedCanvas();
        ConnectionViewModel sourceWire = Assert.Single(sourceCanvas.Connections);
        sourceWire.RoutingMode = CanvasWireRoutingMode.Orthogonal;
        sourceWire.SetBreakpoints([new WireBreakpoint(new Point(160, 80))]);
        sourceWire.RoutingMode = CanvasWireRoutingMode.Straight;

        string json = CanvasSerializer.Serialize(sourceCanvas);

        using var loadedCanvas = new CanvasViewModel();
        CanvasLoadResult load = CanvasSerializer.Deserialize(json, loadedCanvas);

        Assert.True(load.Success, load.Error);
        ConnectionViewModel restored = Assert.Single(loadedCanvas.Connections);
        Assert.Equal(CanvasWireRoutingMode.Straight, restored.RoutingMode);
        Assert.Single(restored.Breakpoints);
        Assert.Equal(new Point(160, 80), restored.Breakpoints[0].Position);
    }

    private static CanvasViewModel CreateSimpleConnectedCanvas()
    {
        var canvas = new CanvasViewModel();
        var table = new NodeViewModel("public.orders", [("id", PinDataType.Integer)], new Point(0, 0));
        var sum = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Sum), new Point(260, 0));

        canvas.Nodes.Add(table);
        canvas.Nodes.Add(sum);

        PinViewModel from = table.OutputPins.Single(p => p.Name == "id");
        PinViewModel to = sum.InputPins.Single(p => p.Name == "value");
        canvas.ConnectPins(from, to);

        return canvas;
    }
}
