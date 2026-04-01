using System.ComponentModel;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.SidebarLeft;

public class SidebarViewModelTests
{
    [Fact]
    public void Constructor_AssignsChildViewModels()
    {
        var nodes = new NodesListViewModel((_, _) => { });
        var connection = new ConnectionManagerViewModel();
        var schema = new SchemaViewModel();

        var vm = new SidebarViewModel(nodes, connection, schema);

        Assert.Same(nodes, vm.NodesList);
        Assert.Same(connection, vm.ConnectionManager);
        Assert.Same(schema, vm.Schema);
    }

    [Fact]
    public void DefaultTab_IsNodes_AndVisibilityFlagsMatch()
    {
        var vm = CreateViewModel();

        Assert.Equal(SidebarTab.Nodes, vm.ActiveTab);
        Assert.True(vm.ShowNodes);
        Assert.False(vm.ShowConnection);
        Assert.False(vm.ShowSchema);
    }

    [Fact]
    public void ActiveTab_Change_UpdatesVisibilityFlags_AndRaisesProperties()
    {
        var vm = CreateViewModel();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.PropertyName))
                raised.Add(e.PropertyName!);
        };

        vm.ActiveTab = SidebarTab.Connection;

        Assert.Equal(SidebarTab.Connection, vm.ActiveTab);
        Assert.False(vm.ShowNodes);
        Assert.True(vm.ShowConnection);
        Assert.False(vm.ShowSchema);

        Assert.Contains(nameof(SidebarViewModel.ActiveTab), raised);
        Assert.Contains(nameof(SidebarViewModel.ShowNodes), raised);
        Assert.Contains(nameof(SidebarViewModel.ShowConnection), raised);
        Assert.Contains(nameof(SidebarViewModel.ShowSchema), raised);
    }

    [Fact]
    public void ActiveTab_SetSameValue_DoesNotRaiseDerivedVisibilityProperties()
    {
        var vm = CreateViewModel();
        int derivedChanges = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SidebarViewModel.ShowNodes)
                or nameof(SidebarViewModel.ShowConnection)
                or nameof(SidebarViewModel.ShowSchema))
            {
                derivedChanges++;
            }
        };

        vm.ActiveTab = SidebarTab.Nodes;

        Assert.Equal(0, derivedChanges);
    }

    private static SidebarViewModel CreateViewModel() =>
        new(
            new NodesListViewModel((_, _) => { }),
            new ConnectionManagerViewModel(),
            new SchemaViewModel()
        );
}
