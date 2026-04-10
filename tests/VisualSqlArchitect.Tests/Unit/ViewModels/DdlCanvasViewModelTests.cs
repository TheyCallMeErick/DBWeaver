using DBWeaver.UI.Services.Benchmark;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class CanvasViewModelTests
{
    [Fact]
    public void IsEmpty_TrueWhenNoNodesOrConnections()
    {
        var vm = new CanvasViewModel();

        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public void IsEmpty_BecomesFalse_WhenNodeIsAdded()
    {
        var vm = new CanvasViewModel();
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(0, 0));

        vm.Nodes.Add(node);

        Assert.False(vm.IsEmpty);
    }
}

