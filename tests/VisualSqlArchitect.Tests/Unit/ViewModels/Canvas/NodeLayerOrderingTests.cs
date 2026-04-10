using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;
using DBWeaver.UI.ViewModels.UndoRedo.Commands;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class NodeLayerOrderingTests
{
    private static NodeViewModel Node(string name, int z, bool selected = false)
    {
        var node = new NodeViewModel($"public.{name}", [], new Point(z * 100, 0))
        {
            ZOrder = z,
            IsSelected = selected,
        };
        return node;
    }

    [Fact]
    public void BringForward_SwapsWithImmediateFrontNode()
    {
        var a = Node("a", 0, selected: false);
        var b = Node("b", 1, selected: true);
        var c = Node("c", 2, selected: false);

        var ordered = NodeLayerOrdering.BringForward([a, b, c]);

        Assert.Equal([a, c, b], ordered);
    }

    [Fact]
    public void SendBackward_SwapsWithImmediateBackNode()
    {
        var a = Node("a", 0, selected: false);
        var b = Node("b", 1, selected: true);
        var c = Node("c", 2, selected: false);

        var ordered = NodeLayerOrdering.SendBackward([a, b, c]);

        Assert.Equal([b, a, c], ordered);
    }

    [Fact]
    public void BringToFront_PreservesRelativeOrder()
    {
        var a = Node("a", 0, selected: true);
        var b = Node("b", 1, selected: false);
        var c = Node("c", 2, selected: true);
        var d = Node("d", 3, selected: false);

        var ordered = NodeLayerOrdering.BringToFront([a, b, c, d]);

        Assert.Equal([b, d, a, c], ordered);
    }

    [Fact]
    public void ReorderNodesCommand_UndoRedo_RestoresLayering()
    {
        var vm = new CanvasViewModel();
        vm.Nodes.Clear();
        vm.Connections.Clear();
        vm.UndoRedo.Clear();

        var a = Node("a", 0, selected: false);
        var b = Node("b", 1, selected: true);
        var c = Node("c", 2, selected: false);

        vm.Nodes.Add(a);
        vm.Nodes.Add(b);
        vm.Nodes.Add(c);

        var from = vm.Nodes.ToDictionary(n => n, n => n.ZOrder);
        var to = NodeLayerOrdering.BuildNormalizedMap(NodeLayerOrdering.BringToFront(vm.Nodes));

        vm.UndoRedo.Execute(new ReorderNodesCommand("Bring to front", from, to));
        Assert.Equal(2, b.ZOrder);

        vm.UndoRedo.Undo();
        Assert.Equal(1, b.ZOrder);

        vm.UndoRedo.Redo();
        Assert.Equal(2, b.ZOrder);
    }

    [Fact]
    public void CanvasViewModel_BringForward_AndNormalizeLayers_Work()
    {
        var vm = new CanvasViewModel();
        vm.Nodes.Clear();
        vm.Connections.Clear();
        vm.UndoRedo.Clear();

        var a = Node("a", 0, selected: false);
        var b = Node("b", 1, selected: true);
        var c = Node("c", 2, selected: false);

        vm.Nodes.Add(a);
        vm.Nodes.Add(b);
        vm.Nodes.Add(c);

        bool moved = vm.BringSelectionForward();
        Assert.True(moved);
        Assert.Equal(2, b.ZOrder);

        // break compactness intentionally
        a.ZOrder = 10;
        b.ZOrder = 30;
        c.ZOrder = 20;

        bool normalized = vm.NormalizeLayers();
        Assert.True(normalized);

        List<int> z = vm.Nodes.OrderBy(n => n.ZOrder).Select(n => n.ZOrder).ToList();
        Assert.Equal([0, 1, 2], z);
    }
}


