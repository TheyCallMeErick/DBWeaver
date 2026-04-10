using System.IO;
using Xunit;

namespace DBWeaver.Tests.Unit.Controls;

public class SidebarControlCodeBehindRegressionTests
{
    [Fact]
    public void SidebarCodeBehind_WiresButtonsToActiveTab()
    {
        string source = ReadSidebarCodeBehind();

        Assert.Contains("if (_buttonsWired || DataContext is not SidebarViewModel vm)", source);
        Assert.Contains("nodesButton.Click += (_, _) => vm.ActiveTab = ESidebarTab.Nodes;", source);
        Assert.Contains("connectionButton.Click += (_, _) => vm.ActiveTab = ESidebarTab.Connection;", source);
        Assert.Contains("schemaButton.Click += (_, _) => vm.ActiveTab = ESidebarTab.Schema;", source);
        Assert.DoesNotContain("DiagnosticsTabButton", source);
    }

    [Fact]
    public void SidebarCodeBehind_AssignsChildDataContexts_AndRewiresOnDataContextChange()
    {
        string source = ReadSidebarCodeBehind();

        Assert.Contains("nodesControl.DataContext = vm.NodesList;", source);
        Assert.Contains("connectionControl.DataContext = vm.ConnectionManager;", source);
        Assert.Contains("schemaControl.DataContext = vm.Schema;", source);
        Assert.DoesNotContain("diagnosticsControl", source);

        Assert.Contains("_buttonsWired = false;", source);
        Assert.Contains("WireUpButtons();", source);
        Assert.Contains("_ = AnimateActiveTabAsync(vm.ActiveTab);", source);
        Assert.Contains("search?.Focus();", source);
        Assert.Contains("OnSidebarPropertyChanged", source);
    }

    private static string ReadSidebarCodeBehind()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "DBWeaver.UI",
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
