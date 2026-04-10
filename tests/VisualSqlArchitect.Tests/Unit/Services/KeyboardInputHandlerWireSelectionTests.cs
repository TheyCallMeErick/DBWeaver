using Avalonia.Input;
using DBWeaver.UI.Services;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Services;

public sealed class KeyboardInputHandlerWireSelectionTests
{
    [Fact]
    public void DeleteShortcut_PrioritizesSelectedWire_WithoutDeletingNodes()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();

        int nodeCountBefore = vm.Nodes.Count;
        ConnectionViewModel selectedWire = vm.Connections.First(c => c.ToPin is not null);
        vm.SelectConnection(selectedWire);

        var sut = new KeyboardInputHandler(vm);
        bool handled = sut.HandleShortcut(Key.Delete, KeyModifiers.None);

        Assert.True(handled);
        Assert.Equal(nodeCountBefore, vm.Nodes.Count);
        Assert.DoesNotContain(vm.Connections, c => ReferenceEquals(c, selectedWire));
        Assert.Null(vm.SelectedConnection);
    }
}
