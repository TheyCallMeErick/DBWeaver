using DBWeaver.CanvasKit;

namespace DBWeaver.Tests.Unit.CanvasLib;

public sealed class CanvasLayerOrderingTests
{
    [Fact]
    public void BringForward_SwapsOnlyAcrossUnselectedNeighbor()
    {
        var a = new FakeLayerNode { Name = "a", ZOrder = 0, IsSelected = true };
        var b = new FakeLayerNode { Name = "b", ZOrder = 1, IsSelected = false };
        var c = new FakeLayerNode { Name = "c", ZOrder = 2, IsSelected = false };

        List<FakeLayerNode> ordered = CanvasLayerOrdering.BringForward([a, b, c]);

        Assert.Equal(["b", "a", "c"], ordered.Select(n => n.Name).ToArray());
    }

    [Fact]
    public void SendToBack_MovesSelectedNodesToBeginning()
    {
        var a = new FakeLayerNode { Name = "a", ZOrder = 0, IsSelected = false };
        var b = new FakeLayerNode { Name = "b", ZOrder = 1, IsSelected = true };
        var c = new FakeLayerNode { Name = "c", ZOrder = 2, IsSelected = false };

        List<FakeLayerNode> ordered = CanvasLayerOrdering.SendToBack([a, b, c]);

        Assert.Equal(["b", "a", "c"], ordered.Select(n => n.Name).ToArray());
    }

    [Fact]
    public void BuildNormalizedMap_AssignsSequentialZOrder()
    {
        var a = new FakeLayerNode { Name = "a", ZOrder = 10 };
        var b = new FakeLayerNode { Name = "b", ZOrder = 99 };

        Dictionary<FakeLayerNode, int> map = CanvasLayerOrdering.BuildNormalizedMap([a, b]);

        Assert.Equal(0, map[a]);
        Assert.Equal(1, map[b]);
    }

    private sealed class FakeLayerNode : ICanvasLayerNode
    {
        public required string Name { get; init; }
        public bool IsSelected { get; init; }
        public int ZOrder { get; init; }
    }
}
