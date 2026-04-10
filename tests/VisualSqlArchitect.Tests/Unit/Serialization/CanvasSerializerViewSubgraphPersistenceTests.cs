using System.Text.Json;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.Serialization;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Serialization;

public class CanvasSerializerViewSubgraphPersistenceTests
{
    [Fact]
    public void SerializeWorkspace_DdlViewDefinition_PersistsDedicatedViewSubgraph()
    {
        var queryVm = new CanvasViewModel();
        var ddlVm = new CanvasViewModel();

        var viewNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ViewDefinition), new Point(180, 120));
        viewNode.Parameters["Schema"] = "public";
        viewNode.Parameters["ViewName"] = "v_orders";
        viewNode.Parameters[CanvasSerializer.ViewFromTableParameterKey] = "public.orders";
        viewNode.Parameters[CanvasSerializer.ViewSubgraphParameterKey] = BuildSubgraphJson();

        ddlVm.Nodes.Add(viewNode);

        string workspaceJson = CanvasSerializer.SerializeWorkspace(queryVm, ddlVm, provider: "Postgres", connectionName: "test");

        using JsonDocument doc = JsonDocument.Parse(workspaceJson);
        JsonElement savedNode = doc
            .RootElement
            .GetProperty("DdlCanvas")
            .GetProperty("Nodes")
            .EnumerateArray()
            .Single();

        Assert.True(savedNode.TryGetProperty("ViewSubgraph", out JsonElement viewSubgraph));
        Assert.Equal("public.orders", viewSubgraph.GetProperty("FromTable").GetString());

        JsonElement parameters = savedNode.GetProperty("Parameters");
        Assert.False(parameters.TryGetProperty(CanvasSerializer.ViewSubgraphParameterKey, out _));
    }

    [Fact]
    public void DeserializeWorkspace_DdlViewDefinition_WithDedicatedViewSubgraph_RestoresParameterPayload()
    {
        string payload = BuildSubgraphJson();
        string json = $$"""
        {
          "Version": 4,
          "QueryCanvas": {
            "Version": 3,
            "DatabaseProvider": "Postgres",
            "ConnectionName": "test",
            "Zoom": 1.0,
            "PanX": 0.0,
            "PanY": 0.0,
            "Nodes": [],
            "Connections": [],
            "SelectBindings": [],
            "WhereBindings": []
          },
          "DdlCanvas": {
            "Version": 3,
            "DatabaseProvider": "Postgres",
            "ConnectionName": "test",
            "Zoom": 1.0,
            "PanX": 0.0,
            "PanY": 0.0,
            "Nodes": [
              {
                "NodeId": "view1",
                "NodeType": "ViewDefinition",
                "X": 100,
                "Y": 120,
                "Parameters": {
                  "Schema": "public",
                  "ViewName": "v_orders"
                },
                "PinLiterals": {},
                "ViewSubgraph": {
                  "GraphJson": {{JsonSerializer.Serialize(payload)}},
                  "FromTable": "public.orders"
                }
              }
            ],
            "Connections": [],
            "SelectBindings": [],
            "WhereBindings": []
          }
        }
        """;

        var queryVm = new CanvasViewModel();
        var ddlVm = new CanvasViewModel();

        CanvasLoadResult result = CanvasSerializer.DeserializeWorkspace(json, queryVm, ddlVm);

        Assert.True(result.Success);
        NodeViewModel view = Assert.Single(ddlVm.Nodes);
        Assert.Equal(NodeType.ViewDefinition, view.Type);
        Assert.True(view.Parameters.TryGetValue(CanvasSerializer.ViewSubgraphParameterKey, out string? restored));
        Assert.Equal(payload, restored);
        Assert.Equal("public.orders", view.Parameters[CanvasSerializer.ViewFromTableParameterKey]);
    }

    [Fact]
    public void DeserializeWorkspace_QueryCanvas_SkipsLegacyDdlNodes()
    {
        string json =
            """
            {
              "Version": 4,
              "QueryCanvas": {
                "Version": 3,
                "DatabaseProvider": "Postgres",
                "ConnectionName": "test",
                "Zoom": 1.0,
                "PanX": 0.0,
                "PanY": 0.0,
                "Nodes": [
                  {
                    "NodeId": "q_select",
                    "NodeType": "SelectOutput",
                    "X": 100,
                    "Y": 100,
                    "Parameters": {},
                    "PinLiterals": {}
                  },
                  {
                    "NodeId": "q_ddl",
                    "NodeType": "TableDefinition",
                    "X": 180,
                    "Y": 100,
                    "Parameters": {},
                    "PinLiterals": {}
                  }
                ],
                "Connections": [],
                "SelectBindings": [],
                "WhereBindings": []
              },
              "DdlCanvas": {
                "Version": 3,
                "DatabaseProvider": "Postgres",
                "ConnectionName": "test",
                "Zoom": 1.0,
                "PanX": 0.0,
                "PanY": 0.0,
                "Nodes": [],
                "Connections": [],
                "SelectBindings": [],
                "WhereBindings": []
              }
            }
            """;

        var queryVm = new CanvasViewModel();
        var ddlVm = new CanvasViewModel();

        CanvasLoadResult result = CanvasSerializer.DeserializeWorkspace(json, queryVm, ddlVm);

        Assert.True(result.Success);
        NodeViewModel node = Assert.Single(queryVm.Nodes);
        Assert.Equal(NodeType.SelectOutput, node.Type);
        Assert.NotNull(result.Warnings);
        Assert.Contains(
            result.Warnings!,
            w => w.Contains("opposite canvas family", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void DeserializeWorkspace_DdlCanvas_SkipsLegacyQueryNodes()
    {
        string json =
            """
            {
              "Version": 4,
              "QueryCanvas": {
                "Version": 3,
                "DatabaseProvider": "Postgres",
                "ConnectionName": "test",
                "Zoom": 1.0,
                "PanX": 0.0,
                "PanY": 0.0,
                "Nodes": [],
                "Connections": [],
                "SelectBindings": [],
                "WhereBindings": []
              },
              "DdlCanvas": {
                "Version": 3,
                "DatabaseProvider": "Postgres",
                "ConnectionName": "test",
                "Zoom": 1.0,
                "PanX": 0.0,
                "PanY": 0.0,
                "Nodes": [
                  {
                    "NodeId": "d_view",
                    "NodeType": "ViewDefinition",
                    "X": 120,
                    "Y": 140,
                    "Parameters": {
                      "Schema": "public",
                      "ViewName": "v_orders"
                    },
                    "PinLiterals": {}
                  },
                  {
                    "NodeId": "d_query",
                    "NodeType": "SelectOutput",
                    "X": 220,
                    "Y": 140,
                    "Parameters": {},
                    "PinLiterals": {}
                  }
                ],
                "Connections": [],
                "SelectBindings": [],
                "WhereBindings": []
              }
            }
            """;

        var queryVm = new CanvasViewModel();
        var ddlVm = new CanvasViewModel();

        CanvasLoadResult result = CanvasSerializer.DeserializeWorkspace(json, queryVm, ddlVm);

        Assert.True(result.Success);
        NodeViewModel node = Assert.Single(ddlVm.Nodes);
        Assert.Equal(NodeType.ViewDefinition, node.Type);
        Assert.NotNull(result.Warnings);
        Assert.Contains(
            result.Warnings!,
            w => w.Contains("DDL: Skipped", StringComparison.OrdinalIgnoreCase)
                && w.Contains("opposite canvas family", StringComparison.OrdinalIgnoreCase)
        );
    }

    private static string BuildSubgraphJson()
    {
        var graph = new NodeGraph
        {
            Nodes =
            [
                new NodeInstance(
                    "tbl",
                    NodeType.TableSource,
                    new Dictionary<string, string>(),
                    new Dictionary<string, string>(),
                    TableFullName: "public.orders"
                ),
            ],
            SelectOutputs = [new SelectBinding("tbl", "status")],
        };

        return JsonSerializer.Serialize(graph);
    }
}
