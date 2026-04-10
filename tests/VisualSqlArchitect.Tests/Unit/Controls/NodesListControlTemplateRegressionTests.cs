using System.IO;
using Xunit;

namespace DBWeaver.Tests.Unit.Controls;

public class NodesListControlTemplateRegressionTests
{
    [Fact]
    public void NodesTemplate_DefinesEmptySearchState()
    {
        string xaml = ReadNodesXaml();

        Assert.Contains("Classes=\"nodes-empty-state\"", xaml);
        Assert.Contains("IsVisible=\"{Binding HasResults}\"", xaml);
        Assert.Contains("HasResults, Converter={x:Static BoolConverters.Not}", xaml);
        Assert.Contains("Text=\"{Binding [nodesList.empty], Source={x:Static loc:LocalizationService.Instance}}\"", xaml);
    }

    [Fact]
    public void NodesTemplate_KeepsCardHoverState()
    {
        string xaml = ReadNodesXaml();

        Assert.Contains("Style Selector=\"Border.node-card:pointerover\"", xaml);
        Assert.Contains("Style Selector=\"Button.node-card-button:pointerover Border.node-card\"", xaml);
    }

    private static string ReadNodesXaml()
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
                "NodesListControl.axaml"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate NodesListControl.axaml from test base directory.");
    }
}
