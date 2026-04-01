using System.IO;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Controls;

public class SidebarControlCodeBehindRegressionTests
{
    [Fact]
    public void SidebarCodeBehind_WiresButtonsToActiveTab()
    {
        string source = ReadSidebarCodeBehind();

        Assert.Contains("if (_buttonsWired || DataContext is not SidebarViewModel vm)", source);
        Assert.Contains("nodesButton.Click += (_, _) => vm.ActiveTab = SidebarTab.Nodes;", source);
        Assert.Contains("connectionButton.Click += (_, _) => vm.ActiveTab = SidebarTab.Connection;", source);
        Assert.Contains("schemaButton.Click += (_, _) => vm.ActiveTab = SidebarTab.Schema;", source);
    }

    [Fact]
    public void SidebarCodeBehind_AssignsChildDataContexts_AndRewiresOnDataContextChange()
    {
        string source = ReadSidebarCodeBehind();

        Assert.Contains("nodesControl.DataContext = vm.NodesList;", source);
        Assert.Contains("connectionControl.DataContext = vm.ConnectionManager;", source);
        Assert.Contains("schemaControl.DataContext = vm.Schema;", source);

        Assert.Contains("_buttonsWired = false;", source);
        Assert.Contains("WireUpButtons();", source);
    }

    private static string ReadSidebarCodeBehind()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "VisualSqlArchitect.UI",
                "Controls",
                "SidebarLeft",
                "SidebarControl.axaml.cs"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate SidebarControl.axaml.cs from test base directory.");
    }
}
