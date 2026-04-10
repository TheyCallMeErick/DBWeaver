using DBWeaver.UI.Services.Benchmark;
using Avalonia;
using System.Text.Json;
using DBWeaver.Core;
using DBWeaver.Nodes;
using DBWeaver.UI.Serialization;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels;

public class ViewSubcanvasEditorTests
{
    [Fact]
    public async Task EnterViewEditor_LoadsSubgraphAndSetsViewEditorState()
    {
        var vm = BuildCanvasWithViewSubgraph(out NodeViewModel viewNode);

        bool entered = await vm.EnterViewEditorAsync(viewNode);

        Assert.True(entered);
        Assert.True(vm.IsInViewEditor);
        Assert.True(vm.IsInCteEditor);
        Assert.Contains("View", vm.CteEditorBreadcrumb, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("v_orders", vm.CteEditorBreadcrumb, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(vm.Nodes, n => n.Type == NodeType.ResultOutput || n.Type == NodeType.SelectOutput);
    }

    [Fact]
    public async Task ExitViewEditor_PersistsViewGraphAndRestoresParentCanvas()
    {
        var vm = BuildCanvasWithViewSubgraph(out NodeViewModel originalViewNode);

        Assert.True(await vm.EnterViewEditorAsync(originalViewNode));

        bool exited = await vm.ExitCteEditorAsync();

        Assert.True(exited);
        Assert.False(vm.IsInViewEditor);
        Assert.False(vm.IsInCteEditor);
        Assert.Equal(string.Empty, vm.CteEditorBreadcrumb);

        NodeViewModel restoredView = Assert.Single(vm.Nodes, n => n.Type == NodeType.ViewDefinition);
        Assert.True(restoredView.Parameters.ContainsKey(CanvasSerializer.ViewSubgraphParameterKey));
        Assert.True(restoredView.Parameters.ContainsKey(CanvasSerializer.ViewFromTableParameterKey));
        Assert.Equal("public.orders", restoredView.Parameters[CanvasSerializer.ViewFromTableParameterKey]);
        Assert.True(restoredView.Parameters.TryGetValue("SelectSql", out string? selectSql));
        Assert.Contains("SELECT", selectSql ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnterSelectedEditor_OpensViewSubcanvasForSelectedViewDefinition()
    {
        var vm = BuildCanvasWithViewSubgraph(out NodeViewModel viewNode);
        viewNode.IsSelected = true;

        bool entered = await vm.EnterSelectedCteEditorAsync();

        Assert.True(entered);
        Assert.True(vm.IsInViewEditor);
        Assert.Contains("View", vm.CteEditorBreadcrumb, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(vm.EditorExitLabel);
    }

    private static CanvasViewModel BuildCanvasWithViewSubgraph(out NodeViewModel viewNode)
    {
        var vm = new CanvasViewModel();
        vm.Nodes.Clear();
        vm.Connections.Clear();
        vm.UndoRedo.Clear();
        vm.ActiveConnectionConfig = new ConnectionConfig(
            DatabaseProvider.Postgres,
            "localhost",
            5432,
            "db",
            "u",
            "p"
        );

        var graph = new NodeGraph
        {
            Nodes =
            [
                new NodeInstance(
                    "tbl",
                    NodeType.TableSource,
                    new Dictionary<string, string>(),
                    new Dictionary<string, string>(),
                    TableFullName: "public.orders",
                    ColumnPins: new Dictionary<string, string> { ["id"] = "id" },
                    ColumnPinTypes: new Dictionary<string, PinDataType> { ["id"] = PinDataType.Number }
                ),
                new NodeInstance(
                    "out",
                    NodeType.ResultOutput,
                    new Dictionary<string, string>(),
                    new Dictionary<string, string>()
                ),
            ],
            Connections =
            [
                new Connection("tbl", "id", "out", "column"),
            ],
            SelectOutputs = [new SelectBinding("tbl", "id")],
        };

        viewNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ViewDefinition), new Point(180, 120));
        viewNode.Parameters["Schema"] = "public";
        viewNode.Parameters["ViewName"] = "v_orders";
        viewNode.Parameters[CanvasSerializer.ViewFromTableParameterKey] = "public.orders";
        viewNode.Parameters[CanvasSerializer.ViewSubgraphParameterKey] = JsonSerializer.Serialize(graph);

        vm.Nodes.Add(viewNode);
        return vm;
    }
}

