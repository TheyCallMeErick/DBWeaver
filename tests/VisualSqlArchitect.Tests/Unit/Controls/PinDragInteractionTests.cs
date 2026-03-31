using Avalonia;
using Avalonia.Controls;
using VisualSqlArchitect.UI.Controls;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Controls;

public class PinDragInteractionTests
{
    [Fact]
    public void CancelDrag_WhileRerouting_KeepsOriginalConnection()
    {
        var vm = new CanvasViewModel();
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
