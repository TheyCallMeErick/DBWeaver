using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

/// <summary>
/// Unit tests for the FitToScreen command in <see cref="NodeLayoutManager"/>.
///
/// Regression test for FRAGILITY_REPORT Â§4 (MÃ©dio):
///   "FitToScreen nao calcula o bounding box real dos nos. Usa valores fixos
///    (85% zoom, offset 80,80) independentemente de onde os nos estao posicionados.
///    Em um canvas com nos em coordenadas 5000,5000, o FitToScreen nao vai mostrar nada."
///
/// Fix: FitToScreen now:
///   1. Computes the real bounding box of all nodes using their Position and Width.
///   2. Derives a zoom level to fit that bounding box inside the stored viewport size.
///   3. Centers the content in the viewport.
///
/// All tests work with plain CanvasViewModel (no Avalonia window required).
/// </summary>
public class FitToScreenTests
{
    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Creates a canvas whose demo nodes are cleared and returns a fresh vm with
    /// a fixed viewport size of 1200Ã—800 set on its layout manager.
    /// </summary>
    private static CanvasViewModel FreshCanvas(double vw = 1200, double vh = 800)
    {
        var vm = new CanvasViewModel();
        vm.Nodes.Clear();
        vm.Connections.Clear();
        vm.UndoRedo.Clear();
        vm.SetViewportSize(vw, vh);
        return vm;
    }

    /// <summary>
    /// Executes FitToScreen via the public command binding.
    /// </summary>
    private static void RunFit(CanvasViewModel vm) =>
        vm.FitToScreenCommand.Execute(null);

    /// <summary>
    /// Returns the screen-space position of a canvas point given zoom + pan.
    /// </summary>
    private static Point ToScreen(CanvasViewModel vm, Point canvas) =>
        vm.CanvasToScreen(canvas);

    // â”€â”€ Empty canvas â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void FitToScreen_EmptyCanvas_IsNoOp()
    {
        var vm = FreshCanvas();
        double zoomBefore = vm.Zoom;
        Point panBefore = vm.PanOffset;

        RunFit(vm);

        // No nodes â†’ zoom and pan must not change
        Assert.Equal(zoomBefore, vm.Zoom);
        Assert.Equal(panBefore, vm.PanOffset);
    }

    // â”€â”€ Nodes near origin â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void FitToScreen_NodesNearOrigin_ZoomIsReasonable()
    {
        var vm = FreshCanvas();
        vm.Nodes.Add(new NodeViewModel("A", [], new Point(0, 0)));
        vm.Nodes.Add(new NodeViewModel("B", [], new Point(300, 0)));
        vm.Nodes.Add(new NodeViewModel("C", [], new Point(0, 200)));

        RunFit(vm);

        // Zoom must be within valid range and not the old hardcoded 0.85
        Assert.InRange(vm.Zoom, 0.15, 2.0);
    }

    [Fact]
    public void FitToScreen_NodesNearOrigin_AllNodesVisibleInViewport()
    {
        var vm = FreshCanvas(1200, 800);
        vm.Nodes.Add(new NodeViewModel("A", [], new Point(0, 0)));
        vm.Nodes.Add(new NodeViewModel("B", [], new Point(400, 0)));
        vm.Nodes.Add(new NodeViewModel("C", [], new Point(200, 300)));

        RunFit(vm);

        // Every node's top-left corner must map to a non-negative screen X and Y
        // and must be within the viewport bounds.
        foreach (NodeViewModel node in vm.Nodes)
        {
            Point screen = ToScreen(vm, node.Position);
            Assert.True(screen.X >= -1, $"Node '{node.Title}' left edge is off-screen left: X={screen.X}");
            Assert.True(screen.Y >= -1, $"Node '{node.Title}' top edge is off-screen top: Y={screen.Y}");
            Assert.True(screen.X <= 1201, $"Node '{node.Title}' left edge is off-screen right: X={screen.X}");
            Assert.True(screen.Y <= 801, $"Node '{node.Title}' top edge is off-screen bottom: Y={screen.Y}");
        }
    }

    // â”€â”€ Nodes far from origin (regression for old bug) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void FitToScreen_NodesFarFromOrigin_NotFixedOffset()
    {
        // Regression: old code set PanOffset = (80, 80) regardless of node positions.
        // With nodes at (5000, 5000), that offset results in nothing visible on screen.
        var vm = FreshCanvas(1200, 800);
        vm.Nodes.Add(new NodeViewModel("Far1", [], new Point(5000, 5000)));
        vm.Nodes.Add(new NodeViewModel("Far2", [], new Point(5300, 5000)));

        RunFit(vm);

        // Pan must NOT be (80, 80) â€” that would leave nodes invisible
        Assert.False(
            vm.PanOffset.X == 80 && vm.PanOffset.Y == 80,
            "PanOffset should not be the old hardcoded (80,80) when nodes are far from origin");
    }

    [Fact]
    public void FitToScreen_NodesFarFromOrigin_BringsNodesIntoView()
    {
        // This is the core regression test: nodes at 5000,5000 must be visible after FitToScreen.
        var vm = FreshCanvas(1200, 800);
        vm.Nodes.Add(new NodeViewModel("Far1", [], new Point(5000, 5000)));
        vm.Nodes.Add(new NodeViewModel("Far2", [], new Point(5400, 5000)));
        vm.Nodes.Add(new NodeViewModel("Far3", [], new Point(5200, 5300)));

        RunFit(vm);

        // After fit, all nodes must have their top-left within the viewport.
        foreach (NodeViewModel node in vm.Nodes)
        {
            Point screen = ToScreen(vm, node.Position);
            Assert.True(screen.X >= -1,
                $"Node at ({node.Position.X},{node.Position.Y}) is still off-screen left after FitToScreen: screenX={screen.X}");
            Assert.True(screen.Y >= -1,
                $"Node at ({node.Position.X},{node.Position.Y}) is still off-screen top after FitToScreen: screenY={screen.Y}");
        }
    }

    [Fact]
    public void FitToScreen_SingleNodeFarFromOrigin_BringsItIntoView()
    {
        var vm = FreshCanvas(1200, 800);
        vm.Nodes.Add(new NodeViewModel("Lone", [], new Point(9000, 7000)));

        RunFit(vm);

        Point screen = ToScreen(vm, vm.Nodes[0].Position);
        Assert.True(screen.X >= -1, $"Single far node is off-screen left: X={screen.X}");
        Assert.True(screen.Y >= -1, $"Single far node is off-screen top: Y={screen.Y}");
    }

    // â”€â”€ Centering â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void FitToScreen_ContentIsCenteredHorizontally()
    {
        // With nodes symmetric around some X center, the viewport center should
        // map to roughly the content center (within 2 * FitPadding tolerance).
        var vm = FreshCanvas(1200, 800);
        vm.Nodes.Add(new NodeViewModel("L", [], new Point(100, 200)));
        vm.Nodes.Add(new NodeViewModel("R", [], new Point(900, 200)));

        RunFit(vm);

        // The midpoint of the content (canvas coords â‰ˆ 500+115 = 615 for x)
        // should project close to the viewport center (600).
        double contentMidX = (100 + 900) / 2.0;
        Point screenMid = ToScreen(vm, new Point(contentMidX, 200));
        Assert.InRange(screenMid.X, 400, 800); // within 200px of viewport center (600)
    }

    [Fact]
    public void FitToScreen_ContentIsCenteredVertically()
    {
        var vm = FreshCanvas(1200, 800);
        vm.Nodes.Add(new NodeViewModel("T", [], new Point(200, 0)));
        vm.Nodes.Add(new NodeViewModel("B", [], new Point(200, 600)));

        RunFit(vm);

        double contentMidY = (0 + 600) / 2.0;
        Point screenMid = ToScreen(vm, new Point(200, contentMidY));
        Assert.InRange(screenMid.Y, 200, 600); // within 200px of viewport center (400)
    }

    // â”€â”€ Zoom limits â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void FitToScreen_VeryLargeCanvas_ZoomClampsToMinimum()
    {
        var vm = FreshCanvas(1200, 800);
        // Spread nodes over 50000 canvas units â€” zoom must not go below 0.15
        for (int i = 0; i < 5; i++)
            vm.Nodes.Add(new NodeViewModel($"N{i}", [], new Point(i * 10000, 0)));

        RunFit(vm);

        Assert.True(vm.Zoom >= 0.15, $"Zoom went below minimum: {vm.Zoom}");
    }

    [Fact]
    public void FitToScreen_SingleTinyNode_ZoomClampsToMax()
    {
        var vm = FreshCanvas(1200, 800);
        vm.Nodes.Add(new NodeViewModel("Tiny", [], new Point(50, 50)));

        RunFit(vm);

        // A very small content area should not zoom beyond the clamped max (2.0)
        Assert.True(vm.Zoom <= 2.0, $"Zoom exceeded maximum: {vm.Zoom}");
    }

    // â”€â”€ SetViewportSize â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void SetViewportSize_ZeroValues_DoNotUpdateStoredSize()
    {
        // Viewport size of 0 is invalid â€” must be ignored to prevent divide-by-zero.
        var vm = FreshCanvas(1200, 800);
        vm.Nodes.Add(new NodeViewModel("A", [], new Point(100, 100)));

        RunFit(vm);
        double zoomWithGoodViewport = vm.Zoom;

        // Providing 0 should be rejected (internal size stays 1200Ã—800)
        vm.SetViewportSize(0, 0);
        RunFit(vm);

        Assert.Equal(zoomWithGoodViewport, vm.Zoom, precision: 6);
    }

    [Fact]
    public void SetViewportSize_LargerViewport_ProducesHigherZoom()
    {
        // Larger viewport â†’ same content fits at higher zoom.
        var vmSmall = FreshCanvas(600, 400);
        vmSmall.Nodes.Add(new NodeViewModel("A", [], new Point(0, 0)));
        vmSmall.Nodes.Add(new NodeViewModel("B", [], new Point(400, 300)));
        RunFit(vmSmall);

        var vmLarge = FreshCanvas(1800, 1200);
        vmLarge.Nodes.Add(new NodeViewModel("A", [], new Point(0, 0)));
        vmLarge.Nodes.Add(new NodeViewModel("B", [], new Point(400, 300)));
        RunFit(vmLarge);

        Assert.True(vmLarge.Zoom > vmSmall.Zoom,
            $"Larger viewport ({vmLarge.Zoom:F3}) should produce higher zoom than smaller ({vmSmall.Zoom:F3})");
    }

    // â”€â”€ Regression: no hardcoded values â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RegressionTest_FitToScreen_DoesNotUseHardcodedZoom085()
    {
        // Old bug: Zoom was hardcoded to 0.85 regardless of content.
        // A very large spread of nodes should produce a different (smaller) zoom.
        var vm = FreshCanvas(1200, 800);
        for (int i = 0; i < 4; i++)
            vm.Nodes.Add(new NodeViewModel($"N{i}", [], new Point(i * 600, i * 400)));

        RunFit(vm);

        // With 4 nodes spread over 1800Ã—1200 canvas units and a 1200Ã—800 viewport,
        // the zoom will be less than 0.85 (content is bigger than viewport).
        Assert.NotEqual(0.85, vm.Zoom, precision: 2);
    }

    [Fact]
    public void RegressionTest_FitToScreen_DoesNotUseHardcodedPan8080()
    {
        // Old bug: PanOffset was hardcoded to (80, 80).
        // With nodes at negative coordinates, that offset leaves them invisible.
        var vm = FreshCanvas(1200, 800);
        vm.Nodes.Add(new NodeViewModel("NegX", [], new Point(-500, -300)));
        vm.Nodes.Add(new NodeViewModel("PosX", [], new Point(100, 100)));

        RunFit(vm);

        Assert.False(
            Math.Abs(vm.PanOffset.X - 80) < 1 && Math.Abs(vm.PanOffset.Y - 80) < 1,
            "PanOffset should not be the old hardcoded (80, 80) â€” nodes at negative coords would be invisible");
    }

    [Fact]
    public void RegressionTest_NodesAtNegativeCoords_VisibleAfterFit()
    {
        // Nodes at negative canvas coordinates must be brought into view.
        var vm = FreshCanvas(1200, 800);
        vm.Nodes.Add(new NodeViewModel("NegA", [], new Point(-800, -600)));
        vm.Nodes.Add(new NodeViewModel("NegB", [], new Point(-400, -200)));

        RunFit(vm);

        foreach (NodeViewModel node in vm.Nodes)
        {
            Point screen = ToScreen(vm, node.Position);
            Assert.True(screen.X >= -1,
                $"Node at negative X={node.Position.X} is still off-screen after FitToScreen: screenX={screen.X}");
            Assert.True(screen.Y >= -1,
                $"Node at negative Y={node.Position.Y} is still off-screen after FitToScreen: screenY={screen.Y}");
        }
    }
}


