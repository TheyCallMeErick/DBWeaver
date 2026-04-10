using System.Text.Json;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.Serialization;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Serialization;

public class CanvasSerializerCteSubgraphPersistenceTests
{
    [Fact]
    public void Serialize_CteDefinition_WithQueryWire_PersistsCteSubgraph()
    {
        var vm = new CanvasViewModel();
        vm.Nodes.Clear();
        vm.Connections.Clear();

        NodeViewModel table = new("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        NodeViewModel colList = new(NodeDefinitionRegistry.Get(NodeType.ColumnList), new Point(120, 0));
        NodeViewModel innerResult = new(NodeDefinitionRegistry.Get(NodeType.ResultOutput), new Point(240, 0));
        NodeViewModel cteDef = new(NodeDefinitionRegistry.Get(NodeType.CteDefinition), new Point(360, 0));

        vm.Nodes.Add(table);
        vm.Nodes.Add(colList);
        vm.Nodes.Add(innerResult);
        vm.Nodes.Add(cteDef);

        Connect(vm, table, "id", colList, "columns");
        Connect(vm, colList, "result", innerResult, "columns");
        Connect(vm, innerResult, "result", cteDef, "query");

        string json = CanvasSerializer.Serialize(vm);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement cteNode = doc
            .RootElement
            .GetProperty("Nodes")
            .EnumerateArray()
            .First(n => n.GetProperty("NodeType").GetString() == nameof(NodeType.CteDefinition));

        Assert.True(cteNode.TryGetProperty("CteSubgraph", out JsonElement cteSubgraph));
        Assert.True(cteSubgraph.GetProperty("Nodes").GetArrayLength() >= 2);
        Assert.True(cteSubgraph.GetProperty("Connections").GetArrayLength() >= 1);
        Assert.False(string.IsNullOrWhiteSpace(cteSubgraph.GetProperty("ResultOutputNodeId").GetString()));
    }

    [Fact]
    public void Deserialize_CteDefinition_WithOnlyPersistedSubgraph_PersistsInsideNode()
    {
        var vm = new CanvasViewModel();

        string json =
            """
            {
              "Version": 3,
              "DatabaseProvider": "Postgres",
              "ConnectionName": "test",
              "Zoom": 1.0,
              "PanX": 0.0,
              "PanY": 0.0,
              "Nodes": [
                {
                  "NodeId": "cte1",
                  "NodeType": "CteDefinition",
                  "X": 100,
                  "Y": 120,
                  "Parameters": { "name": "persisted_cte" },
                  "PinLiterals": {},
                  "CteSubgraph": {
                    "Nodes": [
                      {
                        "NodeId": "inner_result_1",
                        "NodeType": "ResultOutput",
                        "X": 20,
                        "Y": 20,
                        "Parameters": {},
                        "PinLiterals": {}
                      }
                    ],
                    "Connections": [],
                    "ResultOutputNodeId": "inner_result_1"
                  }
                }
              ],
              "Connections": [],
              "SelectBindings": [],
              "WhereBindings": []
            }
            """;

        CanvasLoadResult result = CanvasSerializer.Deserialize(json, vm);

        Assert.True(result.Success);
        NodeViewModel cte = Assert.Single(vm.Nodes);
        Assert.Equal(NodeType.CteDefinition, cte.Type);
        Assert.Empty(vm.Connections);
        Assert.True(cte.Parameters.ContainsKey(CanvasSerializer.CteSubgraphParameterKey));
    }

    private static void Connect(
        CanvasViewModel canvas,
        NodeViewModel fromNode,
        string fromPin,
        NodeViewModel toNode,
        string toPin)
    {
        PinViewModel from = fromNode.OutputPins.First(p => p.Name == fromPin);
        PinViewModel to = toNode.InputPins.First(p => p.Name == toPin);

        canvas.Connections.Add(new ConnectionViewModel(from, from.AbsolutePosition, to.AbsolutePosition)
        {
            ToPin = to,
        });
    }
}
