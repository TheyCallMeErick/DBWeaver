using DBWeaver.UI.ViewModels;
using DBWeaver.Nodes;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.SidebarLeft;

public class NodesListViewModelTests
{
    [Fact]
    public void Constructor_PopulatesInitialGroups()
    {
        var vm = new NodesListViewModel((_, _) => { });

        Assert.NotEmpty(vm.FilteredGroups);
        Assert.True(vm.ShowIntro);
        Assert.All(vm.FilteredGroups, g => Assert.True(g.Items.Count > 0));
    }

    [Fact]
    public void SearchQuery_FiltersGroupsAndItems()
    {
        var vm = new NodesListViewModel((_, _) => { });

        vm.SearchQuery = "upper";

        Assert.False(vm.ShowIntro);
        Assert.NotEmpty(vm.FilteredGroups);
        Assert.All(vm.FilteredGroups, group =>
            Assert.All(group.Items, item =>
                Assert.True(
                    item.Title.Contains("upper", StringComparison.OrdinalIgnoreCase)
                    || item.Subtitle.Contains("upper", StringComparison.OrdinalIgnoreCase)
                    || item.SearchTerms.Any(t => t.Contains("upper", StringComparison.OrdinalIgnoreCase))
                )
            )
        );
    }

    [Fact]
    public void SearchQuery_ByTag_FindsTaggedNodes()
    {
        var vm = new NodesListViewModel((_, _) => { });

        vm.SearchQuery = "report";

        Assert.NotEmpty(vm.FilteredGroups);
        Assert.Contains(
            vm.FilteredGroups.SelectMany(g => g.Items),
            item => item.SearchTerms.Any(t => t.Equals("report", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void SearchQuery_NoMatches_ProducesEmptyGroups()
    {
        var vm = new NodesListViewModel((_, _) => { });

        vm.SearchQuery = "__definitely_no_match__";

        Assert.False(vm.ShowIntro);
        Assert.Empty(vm.FilteredGroups);
    }

    [Fact]
    public void ClearSearchCommand_RestoresIntroAndGroups()
    {
        var vm = new NodesListViewModel((_, _) => { });
        int initialCount = vm.FilteredGroups.Count;

        vm.SearchQuery = "upper";
        Assert.False(vm.ShowIntro);

        vm.ClearSearchCommand.Execute(null);

        Assert.True(vm.ShowIntro);
        Assert.Equal(string.Empty, vm.SearchQuery);
        Assert.Equal(initialCount, vm.FilteredGroups.Count);
    }

    [Fact]
    public void CanvasContext_Ddl_ShowsOnlyDdlCategory()
    {
        var vm = new NodesListViewModel((_, _) => { });

        vm.CanvasContext = CanvasContext.Ddl;

        Assert.NotEmpty(vm.FilteredGroups);
        Assert.All(vm.FilteredGroups, g => Assert.Equal(NodeCategory.Ddl, g.Category));
        Assert.Contains(vm.FilteredGroups, g => g.Name == "Definitions");
        Assert.Contains(vm.FilteredGroups, g => g.Name == "Outputs");
    }

    [Fact]
    public void CanvasContext_Transition_ClearsResidualSearch()
    {
        var vm = new NodesListViewModel((_, _) => { });
        vm.SearchQuery = "join";

        vm.CanvasContext = CanvasContext.Ddl;

        Assert.Equal(string.Empty, vm.SearchQuery);
        Assert.True(vm.ShowIntro);
    }

    [Fact]
    public void NodeItemSpawn_UsesViewportAwareDefaultSentinel()
    {
        NodeDefinition? spawnedDef = null;
        Avalonia.Point spawnedPos = default;
        var vm = new NodesListViewModel((def, pos) =>
        {
            spawnedDef = def;
            spawnedPos = pos;
        });

        NodeTypeItemViewModel firstItem = vm.FilteredGroups.SelectMany(g => g.Items).First();
        firstItem.SpawnNodeCommand.Execute(null);

        Assert.NotNull(spawnedDef);
        Assert.True(double.IsNaN(spawnedPos.X));
        Assert.True(double.IsNaN(spawnedPos.Y));
    }
}
