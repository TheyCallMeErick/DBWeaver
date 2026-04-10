using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.Controls;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Controls;

public sealed class InfiniteCanvasWireDeletionInteractionTests
{
    [Fact]
    public void TryHandleCtrlClickWireDelete_DeletesWireUnderPointer_WhenControlPressed()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        ConnectionViewModel wire = vm.Connections.First(c => c.ToPin is not null);

        bool hitTestCalled = false;
        ConnectionViewModel? deletedWire = null;

        bool handled = InfiniteCanvas.TryHandleCtrlClickWireDelete(
            isControlModifierPressed: true,
            canvasPoint: new Point(150, 150),
            hitTestWire: _ =>
            {
                hitTestCalled = true;
                return wire;
            },
            tryDeleteWire: candidate =>
            {
                deletedWire = candidate;
                return candidate is not null;
            });

        Assert.True(handled);
        Assert.True(hitTestCalled);
        Assert.Same(wire, deletedWire);
    }

    [Fact]
    public void TryHandleCtrlClickWireDelete_DoesNotHitTest_WhenControlIsNotPressed()
    {
        bool hitTestCalled = false;
        bool deleteCalled = false;

        bool handled = InfiniteCanvas.TryHandleCtrlClickWireDelete(
            isControlModifierPressed: false,
            canvasPoint: new Point(150, 150),
            hitTestWire: _ =>
            {
                hitTestCalled = true;
                return null;
            },
            tryDeleteWire: _ =>
            {
                deleteCalled = true;
                return true;
            });

        Assert.False(handled);
        Assert.False(hitTestCalled);
        Assert.False(deleteCalled);
    }

    [Fact]
    public void ResolveWireContextBreakpointAction_ReturnsRemove_WhenPointerHitsBreakpoint()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        ConnectionViewModel wire = vm.Connections.First(c => c.ToPin is not null);
        wire.RoutingMode = CanvasWireRoutingMode.Orthogonal;
        wire.SetBreakpoints(
        [
            new WireBreakpoint(new Point(200, 180)),
            new WireBreakpoint(new Point(240, 220)),
        ]);

        InfiniteCanvas.WireContextBreakpointAction action = InfiniteCanvas.ResolveWireContextBreakpointAction(
            wire,
            canvasPoint: new Point(201, 181));

        Assert.Equal(InfiniteCanvas.WireContextBreakpointActionKind.Remove, action.Kind);
        Assert.Equal(0, action.Index);
        Assert.Equal(new Point(200, 180), action.Position);
    }

    [Fact]
    public void ResolveWireContextBreakpointAction_ReturnsAdd_WhenPointerHitsSegmentWithoutBreakpoint()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        ConnectionViewModel wire = vm.Connections.First(c => c.ToPin is not null);
        wire.RoutingMode = CanvasWireRoutingMode.Orthogonal;
        wire.FromPoint = new Point(120, 180);
        wire.ToPoint = new Point(320, 260);

        InfiniteCanvas.WireContextBreakpointAction action = InfiniteCanvas.ResolveWireContextBreakpointAction(
            wire,
            canvasPoint: new Point(220, 180));

        Assert.Equal(InfiniteCanvas.WireContextBreakpointActionKind.Add, action.Kind);
        Assert.Equal(0, action.Index);
        Assert.Equal(new Point(220, 180), action.Position);
    }

    [Fact]
    public void ResolveWireContextBreakpointAction_ReturnsNone_ForNonOrthogonalWire()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        ConnectionViewModel wire = vm.Connections.First(c => c.ToPin is not null);
        wire.RoutingMode = CanvasWireRoutingMode.Bezier;

        InfiniteCanvas.WireContextBreakpointAction action = InfiniteCanvas.ResolveWireContextBreakpointAction(
            wire,
            canvasPoint: new Point(220, 180));

        Assert.Equal(InfiniteCanvas.WireContextBreakpointActionKind.None, action.Kind);
        Assert.Equal(-1, action.Index);
    }

    [Fact]
    public void ResolveCompatibleWireInsertDefinitions_ReturnsOnlyNodesCompatibleWithWireEndpoints()
    {
        var vm = new CanvasViewModel();
        var source = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ValueNumber), new Point(0, 0));
        var sum = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Sum), new Point(320, 0));
        vm.Nodes.Add(source);
        vm.Nodes.Add(sum);
        vm.ConnectPins(
            source.OutputPins.Single(pin => pin.Name == "result"),
            sum.InputPins.Single(pin => pin.Name == "value"));
        ConnectionViewModel wire = Assert.Single(vm.Connections);

        IReadOnlyList<NodeDefinition> compatible = InfiniteCanvas.ResolveCompatibleWireInsertDefinitions(
            wire,
            CanvasContext.Query,
            limit: 30);

        Assert.NotEmpty(compatible);
        Assert.All(
            compatible,
            definition =>
            {
                var probe = new NodeViewModel(definition, new Point(0, 0));
                bool acceptsInput = probe.InputPins.Any(pin => pin.EvaluateConnection(wire.FromPin).IsAllowed);
                bool providesOutput = probe.OutputPins.Any(pin => wire.ToPin!.EvaluateConnection(pin).IsAllowed);
                Assert.True(acceptsInput && providesOutput);
            });
    }

    [Fact]
    public void TryQuickAddTop100ForResultOutput_AddsNumberAndTopNodes_AndConnectsThem()
    {
        var vm = new CanvasViewModel();
        var resultOutput = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ResultOutput), new Point(400, 200));
        vm.Nodes.Add(resultOutput);

        bool added = InfiniteCanvas.TryQuickAddTop100ForResultOutput(vm, resultOutput);

        Assert.True(added);
        NodeViewModel top = Assert.Single(vm.Nodes, node => node.Type == NodeType.Top);
        NodeViewModel number = Assert.Single(vm.Nodes, node => node.Type == NodeType.ValueNumber);
        Assert.Equal("100", number.Parameters["value"]);

        PinViewModel topInput = resultOutput.InputPins.Single(pin => pin.Name == "top");
        PinViewModel topOutput = top.OutputPins.Single(pin => pin.Name == "result");
        PinViewModel topCount = top.InputPins.Single(pin => pin.Name == "count");
        PinViewModel numberOutput = number.OutputPins.Single(pin => pin.Name == "result");

        Assert.Contains(vm.Connections, connection => ReferenceEquals(connection.FromPin, topOutput) && ReferenceEquals(connection.ToPin, topInput));
        Assert.Contains(vm.Connections, connection => ReferenceEquals(connection.FromPin, numberOutput) && ReferenceEquals(connection.ToPin, topCount));
    }
}
