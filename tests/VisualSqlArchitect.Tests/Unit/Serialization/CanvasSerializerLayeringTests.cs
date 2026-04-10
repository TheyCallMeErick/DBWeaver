using Avalonia;
using DBWeaver.UI.Serialization;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Serialization;

public class CanvasSerializerLayeringTests
{
    private static NodeViewModel Node(string name, Point pos, int z)
    {
        return new NodeViewModel($"public.{name}", [], pos)
        {
            ZOrder = z,
        };
    }

    [Fact]
    public void SaveLoad_RoundTrip_PreservesRelativeLayerOrder()
    {
        var source = new CanvasViewModel();
        source.Nodes.Clear();
        source.Connections.Clear();
        source.UndoRedo.Clear();

        source.Nodes.Add(Node("low", new Point(10, 10), 1));
        source.Nodes.Add(Node("mid", new Point(200, 10), 5));
        source.Nodes.Add(Node("top", new Point(400, 10), 9));

        List<string> before = source.Nodes
            .OrderBy(n => n.ZOrder)
            .Select(n => n.Id)
            .ToList();

        string json = CanvasSerializer.Serialize(source, provider: "Postgres", connectionName: "test");

        var loaded = new CanvasViewModel();
        CanvasLoadResult result = CanvasSerializer.Deserialize(json, loaded);

        Assert.True(result.Success, result.Error ?? "Deserialize failed");

        List<string> after = loaded.Nodes
            .OrderBy(n => n.ZOrder)
            .Select(n => n.Id)
            .ToList();

        Assert.Equal(before, after);
        Assert.Equal(3, loaded.Nodes.Count);

        List<int> z = loaded.Nodes.OrderBy(n => n.ZOrder).Select(n => n.ZOrder).ToList();
        Assert.Equal([0, 1, 2], z);
    }
}
