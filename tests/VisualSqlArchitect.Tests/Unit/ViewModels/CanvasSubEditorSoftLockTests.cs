using DBWeaver.UI.Services.Benchmark;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using DBWeaver.Core;
using DBWeaver.Nodes;
using DBWeaver.UI.Serialization;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels;

public class CanvasSubEditorSoftLockTests
{
    [Fact]
    public async Task ExitViewEditor_WhenCanvasInvalid_AddsWarningAndKeepsSession()
    {
        var vm = BuildCanvasWithViewSubgraph(out NodeViewModel viewNode);
        Assert.True(await vm.EnterViewEditorAsync(viewNode));

        vm.Nodes.Clear();
        vm.Connections.Clear();
        int before = vm.Diagnostics.SnapshotEntries().Count;

        bool exited = await vm.ExitCteEditorAsync();

        Assert.False(exited);
        Assert.True(vm.IsInViewEditor);
        Assert.True(vm.Diagnostics.SnapshotEntries().Count > before);
    }

    [Fact]
    public async Task ExitViewEditor_ForceDiscard_ClearsSessionWhenCanvasInvalid()
    {
        var vm = BuildCanvasWithViewSubgraph(out NodeViewModel viewNode);
        Assert.True(await vm.EnterViewEditorAsync(viewNode));

        vm.Nodes.Clear();
        vm.Connections.Clear();

        bool exited = await vm.ExitCteEditorAsync(forceDiscard: true);

        Assert.True(exited);
        Assert.False(vm.IsInViewEditor);
        Assert.False(vm.IsInCteEditor);
    }

    [Fact]
    public async Task ExitViewEditor_WhenParentRestoreFails_ClearsSessionAndWarns()
    {
        var vm = BuildCanvasWithViewSubgraph(out NodeViewModel viewNode);
        Assert.True(await vm.EnterViewEditorAsync(viewNode));

        CorruptViewEditorSessionParentJson(vm);
        int before = vm.Diagnostics.SnapshotEntries().Count;

        bool exited = await vm.ExitCteEditorAsync(forceDiscard: true);

        Assert.False(exited);
        Assert.False(vm.IsInViewEditor);
        Assert.True(vm.Diagnostics.SnapshotEntries().Count > before);
    }

    [Fact]
    public async Task ExitCteEditor_WhenParentRestoreFails_ClearsSessionAndWarns()
    {
        var vm = BuildCanvasWithCteSubgraph(out NodeViewModel cteNode);
        cteNode.IsSelected = true;
        Assert.True(await vm.EnterSelectedCteEditorAsync());

        CorruptCteEditorSessionParentJson(vm);
        int before = vm.Diagnostics.SnapshotEntries().Count;

        bool exited = await vm.ExitCteEditorAsync();

        Assert.False(exited);
        Assert.False(vm.IsInCteEditor);
        Assert.True(vm.Diagnostics.SnapshotEntries().Count > before);
    }

    [Fact]
    public void CanvasViewModel_SubEditorFlow_DoesNotUseBlockingGetAwaiterResult()
    {
        string source = File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "src", "DBWeaver.UI", "ViewModels", "CanvasViewModel.cs"));

        Assert.DoesNotContain(".GetAwaiter().GetResult()", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Result", source, StringComparison.Ordinal);
    }

    private static void CorruptViewEditorSessionParentJson(CanvasViewModel vm)
    {
        FieldInfo field = typeof(CanvasViewModel).GetField("_viewEditorSession", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object session = field.GetValue(vm)!;
        Type sessionType = session.GetType();

        string parentViewNodeId = (string)sessionType.GetProperty("ParentViewNodeId")!.GetValue(session)!;
        bool parentWasDirty = (bool)sessionType.GetProperty("ParentWasDirty")!.GetValue(session)!;
        string viewName = (string)sessionType.GetProperty("ViewDisplayName")!.GetValue(session)!;

        object brokenSession = Activator.CreateInstance(
            sessionType,
            "{ invalid json",
            parentViewNodeId,
            parentWasDirty,
            viewName
        )!;

        field.SetValue(vm, brokenSession);
    }

    private static void CorruptCteEditorSessionParentJson(CanvasViewModel vm)
    {
        FieldInfo field = typeof(CanvasViewModel).GetField("_cteEditorSession", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object session = field.GetValue(vm)!;
        Type sessionType = session.GetType();

        string parentCteNodeId = (string)sessionType.GetProperty("ParentCteNodeId")!.GetValue(session)!;
        bool parentWasDirty = (bool)sessionType.GetProperty("ParentWasDirty")!.GetValue(session)!;
        string cteDisplayName = (string)sessionType.GetProperty("CteDisplayName")!.GetValue(session)!;

        object brokenSession = Activator.CreateInstance(
            sessionType,
            "{ invalid json",
            parentCteNodeId,
            parentWasDirty,
            cteDisplayName
        )!;

        field.SetValue(vm, brokenSession);
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

    private static CanvasViewModel BuildCanvasWithCteSubgraph(out NodeViewModel cteNode)
    {
        var vm = new CanvasViewModel();
        vm.Nodes.Clear();
        vm.Connections.Clear();
        vm.UndoRedo.Clear();

        NodeViewModel table = new("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        NodeViewModel cteColumns = new(NodeDefinitionRegistry.Get(NodeType.ColumnList), new Point(130, 0));
        NodeViewModel cteResult = new(NodeDefinitionRegistry.Get(NodeType.ResultOutput), new Point(260, 0));
        cteNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.CteDefinition), new Point(400, 0));
        cteNode.Parameters["name"] = "orders_cte";

        vm.Nodes.Add(table);
        vm.Nodes.Add(cteColumns);
        vm.Nodes.Add(cteResult);
        vm.Nodes.Add(cteNode);

        Connect(vm, table, "id", cteColumns, "columns");
        Connect(vm, cteColumns, "result", cteResult, "columns");
        Connect(vm, cteResult, "result", cteNode, "query");
        return vm;
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

