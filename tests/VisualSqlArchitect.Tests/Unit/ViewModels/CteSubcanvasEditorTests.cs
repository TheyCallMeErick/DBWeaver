using Avalonia;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Serialization;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.Tests.Unit.ViewModels;

public class CteSubcanvasEditorTests
{
    [Fact]
    public void EnterSelectedCteEditor_LoadsIsolatedSubgraph()
    {
        var vm = BuildCanvasWithCteSubgraph(out NodeViewModel cteNode, out _);
        cteNode.IsSelected = true;

        bool entered = vm.EnterSelectedCteEditor();

        Assert.True(entered);
        Assert.True(vm.IsInCteEditor);
        Assert.Contains("CTE", vm.CteEditorBreadcrumb, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orders_cte", vm.CteEditorBreadcrumb, StringComparison.OrdinalIgnoreCase);
        Assert.All(vm.Nodes, n => Assert.NotEqual(NodeType.CteDefinition, n.Type));
        Assert.Contains(vm.Nodes, n => n.Type == NodeType.ResultOutput);
        Assert.DoesNotContain(vm.Nodes, n => string.Equals(n.Subtitle, "public.unrelated", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnterCteEditor_EntersWhenNodeIsNotPreselected()
    {
        var vm = BuildCanvasWithCteSubgraph(out NodeViewModel cteNode, out _);

        bool entered = vm.EnterCteEditor(cteNode);

        Assert.True(entered);
        Assert.True(vm.IsInCteEditor);
        Assert.Contains("orders_cte", vm.CteEditorBreadcrumb, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnterCteEditor_ReturnsFalseForNonCteNode()
    {
        var vm = BuildCanvasWithCteSubgraph(out _, out _);
        NodeViewModel nonCte = vm.Nodes.First(n => n.Type != NodeType.CteDefinition);

        bool entered = vm.EnterCteEditor(nonCte);

        Assert.False(entered);
        Assert.False(vm.IsInCteEditor);
    }

    [Fact]
    public void ExitCteEditor_RestoresParentAndReconnectsQueryWire()
    {
        var vm = BuildCanvasWithCteSubgraph(out NodeViewModel cteNode, out string cteId);
        cteNode.IsSelected = true;

        Assert.True(vm.EnterSelectedCteEditor());
        Assert.True(vm.IsInCteEditor);

        bool exited = vm.ExitCteEditor();

        Assert.True(exited);
        Assert.False(vm.IsInCteEditor);
        Assert.Equal(string.Empty, vm.CteEditorBreadcrumb);

        NodeViewModel restoredCte = Assert.Single(vm.Nodes, n => n.Id == cteId);
        Assert.Equal(NodeType.CteDefinition, restoredCte.Type);

        Assert.Contains(vm.Nodes, n => string.Equals(n.Subtitle, "public.unrelated", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(vm.Nodes, n => n.Type == NodeType.ResultOutput);
        Assert.DoesNotContain(vm.Nodes, n => n.Type == NodeType.ColumnList);
        Assert.DoesNotContain(vm.Nodes, n => string.Equals(n.Subtitle, "public.orders", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(vm.Connections, c => c.ToPin?.Owner == restoredCte && c.ToPin.Name == "query");
        Assert.True(restoredCte.Parameters.ContainsKey(CanvasSerializer.CteSubgraphParameterKey));
    }

    [Fact]
    public void ExitAndReenterCteEditor_PreservesEditedSubgraph()
    {
        var vm = new CanvasViewModel();
        vm.Nodes.Clear();
        vm.Connections.Clear();
        vm.UndoRedo.Clear();

        NodeViewModel cteNode = new(NodeDefinitionRegistry.Get(NodeType.CteDefinition), new Point(300, 100));
        cteNode.Parameters["name"] = "orders_cte";
        vm.Nodes.Add(cteNode);

        Assert.True(vm.EnterCteEditor(cteNode));

        NodeViewModel table = vm.SpawnTableNode("public.orders", [("id", PinDataType.Number)], new Point(20, 20));
        NodeViewModel colList = vm.SpawnNode(NodeDefinitionRegistry.Get(NodeType.ColumnList), new Point(180, 20));
        NodeViewModel result = Assert.Single(vm.Nodes, n => n.Type == NodeType.ResultOutput);

        vm.ConnectPins(table.OutputPins.First(p => p.Name == "id"), colList.InputPins.First(p => p.Name == "columns"));
        vm.ConnectPins(colList.OutputPins.First(p => p.Name == "result"), result.InputPins.First(p => p.Name == "columns"));

        Assert.Contains(vm.Connections, c => c.ToPin?.Owner == colList && c.ToPin.Name == "columns");
        Assert.Contains(vm.Connections, c => c.ToPin?.Owner == result && c.ToPin.Name == "columns");

        Assert.True(vm.ExitCteEditor());
        NodeViewModel restoredCte = Assert.Single(vm.Nodes);
        Assert.Equal(NodeType.CteDefinition, restoredCte.Type);
        Assert.True(restoredCte.Parameters.ContainsKey(CanvasSerializer.CteSubgraphParameterKey));

        Assert.True(vm.EnterCteEditor(restoredCte));
        Assert.Contains(vm.Nodes, n => n.Type == NodeType.ResultOutput);
        Assert.Contains(vm.Nodes, n => n.Type == NodeType.ColumnList);
        Assert.Contains(vm.Nodes, n => string.Equals(n.Subtitle, "public.orders", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(vm.Connections, c => c.ToPin?.Owner.Type == NodeType.ColumnList && c.ToPin.Name == "columns");
        Assert.Contains(vm.Connections, c => c.ToPin?.Owner.Type == NodeType.ResultOutput && c.ToPin.Name == "columns");
    }

    private static CanvasViewModel BuildCanvasWithCteSubgraph(out NodeViewModel cteNode, out string cteId)
    {
        var vm = new CanvasViewModel();
        vm.Nodes.Clear();
        vm.Connections.Clear();
        vm.UndoRedo.Clear();

        NodeViewModel unrelated = new("public.unrelated", [("id", PinDataType.Number)], new Point(0, 220));
        NodeViewModel table = new("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        NodeViewModel cteColumns = new(NodeDefinitionRegistry.Get(NodeType.ColumnList), new Point(130, 0));
        NodeViewModel cteResult = new(NodeDefinitionRegistry.Get(NodeType.ResultOutput), new Point(260, 0));
        cteNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.CteDefinition), new Point(400, 0));
        cteNode.Parameters["name"] = "orders_cte";

        vm.Nodes.Add(unrelated);
        vm.Nodes.Add(table);
        vm.Nodes.Add(cteColumns);
        vm.Nodes.Add(cteResult);
        vm.Nodes.Add(cteNode);

        Connect(vm, table, "id", cteColumns, "columns");
        Connect(vm, cteColumns, "result", cteResult, "columns");
        Connect(vm, cteResult, "result", cteNode, "query");

        cteId = cteNode.Id;
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
