using System.ComponentModel;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.SidebarLeft;

public class SidebarViewModelTests
{
    [Fact]
    public void Constructor_AssignsChildViewModels()
    {
        var nodes = new NodesListViewModel((_, _) => { });
        var connection = new ConnectionManagerViewModel();
        var diagnostics = new AppDiagnosticsViewModel(new CanvasViewModel());

        var vm = new SidebarViewModel(nodes, connection, diagnostics);

        Assert.Same(nodes, vm.NodesList);
        Assert.Same(connection, vm.ConnectionManager);
        Assert.Same(diagnostics, vm.Diagnostics);
    }

    [Fact]
    public void DefaultTab_IsNodes_AndVisibilityFlagsMatch()
    {
        var vm = CreateViewModel();

        Assert.Equal(SidebarTab.Nodes, vm.ActiveTab);
        Assert.True(vm.ShowNodes);
        Assert.False(vm.ShowConnection);
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

        Assert.Contains(nameof(SidebarViewModel.ActiveTab), raised);
        Assert.Contains(nameof(SidebarViewModel.ShowNodes), raised);
        Assert.Contains(nameof(SidebarViewModel.ShowConnection), raised);
    }

    [Fact]
    public void ActiveTab_SetSameValue_DoesNotRaiseDerivedVisibilityProperties()
    {
        var vm = CreateViewModel();
        int derivedChanges = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SidebarViewModel.ShowNodes)
                or nameof(SidebarViewModel.ShowConnection))
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
            new AppDiagnosticsViewModel(new CanvasViewModel())
        );

    [Fact]
    public void TabCommands_SwitchTabsCorrectly()
    {
        var vm = CreateViewModel();

        vm.SelectConnectionCommand.Execute(null);
        Assert.Equal(SidebarTab.Connection, vm.ActiveTab);

        vm.SelectNodesCommand.Execute(null);
        Assert.Equal(SidebarTab.Nodes, vm.ActiveTab);
    }

    [Fact]
    public void AddNodeCommand_SetsNodesTab_AndRaisesEvent()
    {
        var vm = CreateViewModel();
        vm.ActiveTab = SidebarTab.Connection;
        bool raised = false;
        vm.AddNodeRequested += () => raised = true;

        vm.AddNodeCommand.Execute(null);

        Assert.Equal(SidebarTab.Nodes, vm.ActiveTab);
        Assert.True(raised);
    }

    [Fact]
    public void TogglePreviewCommand_RaisesEvent()
    {
        var vm = CreateViewModel();
        bool raised = false;
        vm.TogglePreviewRequested += () => raised = true;

        vm.TogglePreviewCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void AddConnectionCommand_SetsConnectionTab_AndRaisesEvent()
    {
        var vm = CreateViewModel();
        vm.ActiveTab = SidebarTab.Nodes;
        bool raised = false;
        vm.AddConnectionRequested += () => raised = true;

        vm.AddConnectionCommand.Execute(null);

        Assert.Equal(SidebarTab.Connection, vm.ActiveTab);
        Assert.True(raised);
    }
}
