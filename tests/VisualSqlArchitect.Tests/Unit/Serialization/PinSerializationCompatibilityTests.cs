using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.Serialization;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Serialization;

public sealed class PinSerializationCompatibilityTests
{
    [Fact]
    public void Serialize_RemovesDeprecatedQueryLegacyParameters()
    {
        using var canvas = new CanvasViewModel();
        var result = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ResultOutput), new Point(0, 0));
        result.Parameters["set_operator"] = "UNION";
        result.Parameters["set_query"] = "SELECT 1";
        result.Parameters["import_order_terms"] = "a|b|ASC";
        result.Parameters["import_group_terms"] = "a|b";
        canvas.Nodes.Add(result);

        string json = CanvasSerializer.Serialize(canvas);

        Assert.DoesNotContain("set_operator", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("set_query", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("import_order_terms", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("import_group_terms", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_RemovesDeprecatedQueryLegacyParameters()
    {
        const string json = """
{
  "Version": 3,
  "Zoom": 1,
  "PanX": 0,
  "PanY": 0,
  "Nodes": [
    {
      "NodeId": "n1",
      "NodeType": "ResultOutput",
      "X": 0,
      "Y": 0,
      "Parameters": {
        "set_operator": "UNION",
        "set_query": "SELECT 1",
        "import_order_terms": "a|b|ASC",
        "import_group_terms": "a|b"
      },
      "PinLiterals": {}
    }
  ],
  "Connections": [],
  "SelectBindings": [],
  "WhereBindings": []
}
""";

        using var loadedCanvas = new CanvasViewModel();
        CanvasLoadResult load = CanvasSerializer.Deserialize(json, loadedCanvas);

        Assert.True(load.Success);
        NodeViewModel restored = Assert.Single(loadedCanvas.Nodes);
        Assert.False(restored.Parameters.ContainsKey("set_operator"));
        Assert.False(restored.Parameters.ContainsKey("set_query"));
        Assert.False(restored.Parameters.ContainsKey("import_order_terms"));
        Assert.False(restored.Parameters.ContainsKey("import_group_terms"));
    }

    [Fact]
    public void SerializeThenDeserialize_PreservesConnectionPinTypesAndEndpoints()
    {
        using var sourceCanvas = new CanvasViewModel();
        var table = new NodeViewModel("public.orders", [("id", PinDataType.Integer)], new Point(0, 0));
        var sum = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Sum), new Point(220, 0));

        sourceCanvas.Nodes.Add(table);
        sourceCanvas.Nodes.Add(sum);

        PinViewModel from = table.OutputPins.Single(p => p.Name == "id");
        PinViewModel to = sum.InputPins.Single(p => p.Name == "value");
        sourceCanvas.ConnectPins(from, to);

        string json = CanvasSerializer.Serialize(sourceCanvas);

        using var loadedCanvas = new CanvasViewModel();
        CanvasLoadResult load = CanvasSerializer.Deserialize(json, loadedCanvas);

        Assert.True(load.Success);
        ConnectionViewModel restored = Assert.Single(loadedCanvas.Connections);
        Assert.Equal("id", restored.FromPin.Name);
        Assert.Equal(PinDataType.ColumnRef, restored.FromPin.DataType);
        Assert.Equal("value", restored.ToPin!.Name);
        Assert.Equal(PinDataType.Number, restored.ToPin.DataType);
    }
}
