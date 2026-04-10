using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class CanvasViewportSpawnPositionTests
{
    [Fact]
    public void NewCanvas_StartsAtOriginWithDefaultZoom()
    {
        var canvas = new CanvasViewModel();

        Assert.Equal(1.0, canvas.Zoom);
        Assert.Equal(new Point(0, 0), canvas.PanOffset);
    }

    [Fact]
    public void ScreenToCanvas_WithPanAndZoom_CanResolveNegativeCoordinates()
    {
        var canvas = new CanvasViewModel
        {
            Zoom = 2.0,
            PanOffset = new Point(600, 400),
            SnapToGrid = false
        };

        Point visibleCenterOnScreen = new(300, 200);
        Point spawnPoint = canvas.ScreenToCanvas(visibleCenterOnScreen);

        Assert.Equal(new Point(-150, -100), spawnPoint);

        canvas.SpawnNode(NodeDefinitionRegistry.Get(NodeType.Equals), spawnPoint);

        NodeViewModel node = Assert.Single(canvas.Nodes);
        Assert.Equal(spawnPoint, node.Position);
    }
}
