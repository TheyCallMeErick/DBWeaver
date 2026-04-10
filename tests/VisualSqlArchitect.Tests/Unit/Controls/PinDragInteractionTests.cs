using Avalonia;
using Avalonia.Controls;
using DBWeaver.Nodes;
using DBWeaver.UI.Controls;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.Controls;

public class PinDragInteractionTests
{
    [Fact]
    public void CancelDrag_WhileRerouting_KeepsOriginalConnection()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        AssignDistinctPinPositions(vm);

        var original = vm.Connections.First(c => c.ToPin is not null);
        var targetPin = original.ToPin!;
        int before = vm.Connections.Count;

        var interaction = new PinDragInteraction(vm, new Canvas());

        interaction.BeginDrag(targetPin, targetPin.AbsolutePosition);
        Assert.Equal(before + 1, vm.Connections.Count); // only pending wire added

        interaction.CancelDrag();

        Assert.Equal(before, vm.Connections.Count);
        Assert.Contains(original, vm.Connections);
    }

    [Fact]
    public void EndDrag_WithoutValidTarget_KeepsOriginalConnection()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        AssignDistinctPinPositions(vm);

        var original = vm.Connections.First(c => c.ToPin is not null);
        var targetPin = original.ToPin!;
        int before = vm.Connections.Count;

        var interaction = new PinDragInteraction(vm, new Canvas());

        interaction.BeginDrag(targetPin, targetPin.AbsolutePosition);
        interaction.EndDrag(new Point(10_000, 10_000));

        Assert.Equal(before, vm.Connections.Count);
        Assert.Contains(original, vm.Connections);
    }

    [Fact]
    public void EndDrag_WithValidTarget_ReroutesConnection()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        AssignDistinctPinPositions(vm);

        var original = vm.Connections.First(c => c.ToPin is not null);
        var originalTarget = original.ToPin!;
        var source = original.FromPin;

        var newTarget = vm.Nodes
            .SelectMany(n => n.InputPins)
            .First(p => !ReferenceEquals(p, originalTarget) && p.CanAccept(source));

        var interaction = new PinDragInteraction(vm, new Canvas());

        interaction.BeginDrag(originalTarget, originalTarget.AbsolutePosition);
        interaction.EndDrag(newTarget.AbsolutePosition);

        Assert.DoesNotContain(original, vm.Connections);
        Assert.Contains(vm.Connections, c => ReferenceEquals(c.FromPin, source) && ReferenceEquals(c.ToPin, newTarget));
    }

    [Fact]
    public void DragPendingWire_DoesNotResetWindowFunctionPartitionSlots()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        AssignDistinctPinPositions(vm);

        NodeViewModel window = vm.SpawnNode(
            NodeDefinitionRegistry.Get(NodeType.WindowFunction),
            new Point(500, 260)
        );

        window.AddWindowPartitionSlot();
        window.AddWindowPartitionSlot();
        int beforePartitions = window.InputPins.Count(p => p.Name.StartsWith("partition_"));

        var original = vm.Connections.First(c => c.ToPin is not null);
        var targetPin = original.ToPin!;
        var interaction = new PinDragInteraction(vm, new Canvas());

        interaction.BeginDrag(targetPin, targetPin.AbsolutePosition);
        interaction.CancelDrag();

        int afterPartitions = window.InputPins.Count(p => p.Name.StartsWith("partition_"));
        Assert.Equal(beforePartitions, afterPartitions);
    }

    [Fact]
    public void DragRerouteCommit_DoesNotResetWindowFunctionPartitionSlots()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        AssignDistinctPinPositions(vm);

        NodeViewModel window = vm.SpawnNode(
            NodeDefinitionRegistry.Get(NodeType.WindowFunction),
            new Point(520, 300)
        );

        window.AddWindowPartitionSlot();
        window.AddWindowPartitionSlot();
        int beforePartitions = window.InputPins.Count(p => p.Name.StartsWith("partition_"));

        var original = vm.Connections.First(c => c.ToPin is not null);
        var originalTarget = original.ToPin!;
        var source = original.FromPin;

        var newTarget = vm.Nodes
            .SelectMany(n => n.InputPins)
            .First(p => !ReferenceEquals(p, originalTarget) && p.CanAccept(source));

        var interaction = new PinDragInteraction(vm, new Canvas());
        interaction.BeginDrag(originalTarget, originalTarget.AbsolutePosition);
        interaction.EndDrag(newTarget.AbsolutePosition);

        int afterPartitions = window.InputPins.Count(p => p.Name.StartsWith("partition_"));
        Assert.Equal(beforePartitions, afterPartitions);
    }

    [Fact]
    public void DragRerouteCommit_DoesNotResetWindowFunctionOrderSlots()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        AssignDistinctPinPositions(vm);

        NodeViewModel window = vm.SpawnNode(
            NodeDefinitionRegistry.Get(NodeType.WindowFunction),
            new Point(540, 320)
        );

        window.AddWindowOrderSlot();
        window.AddWindowOrderSlot();
        int beforeOrders = window.InputPins.Count(p => p.Name.StartsWith("order_"));

        var original = vm.Connections.First(c => c.ToPin is not null);
        var originalTarget = original.ToPin!;
        var source = original.FromPin;

        var newTarget = vm.Nodes
            .SelectMany(n => n.InputPins)
            .First(p => !ReferenceEquals(p, originalTarget) && p.CanAccept(source));

        var interaction = new PinDragInteraction(vm, new Canvas());
        interaction.BeginDrag(originalTarget, originalTarget.AbsolutePosition);
        interaction.EndDrag(newTarget.AbsolutePosition);

        int afterOrders = window.InputPins.Count(p => p.Name.StartsWith("order_"));
        Assert.Equal(beforeOrders, afterOrders);
    }

    private static void AssignDistinctPinPositions(CanvasViewModel vm)
    {
        int i = 0;
        foreach (var pin in vm.Nodes.SelectMany(n => n.AllPins))
        {
            pin.AbsolutePosition = new Point(100 + i * 20, 200 + i * 3);
            i++;
        }
    }
}
