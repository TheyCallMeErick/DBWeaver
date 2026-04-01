using System.IO;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Controls;

public class SidebarControlTemplateRegressionTests
{
    [Fact]
    public void SidebarTemplate_DefinesThreeTabButtons_AndActiveClassBindings()
    {
        string xaml = ReadSidebarXaml();

        Assert.Contains("x:Name=\"NodesTabButton\"", xaml);
        Assert.Contains("x:Name=\"ConnectionTabButton\"", xaml);
        Assert.Contains("x:Name=\"SchemaTabButton\"", xaml);

        Assert.Contains("Classes.active=\"{Binding ShowNodes}\"", xaml);
        Assert.Contains("Classes.active=\"{Binding ShowConnection}\"", xaml);
        Assert.Contains("Classes.active=\"{Binding ShowSchema}\"", xaml);
    }

    [Fact]
    public void SidebarTemplate_DefinesTabContentVisibilityBindings()
    {
        string xaml = ReadSidebarXaml();

        Assert.Contains("<ctrl:NodesListControl x:Name=\"NodesControl\"/>", xaml);
        Assert.Contains("<ctrl:ConnectionTabControl x:Name=\"ConnectionControl\"/>", xaml);
        Assert.Contains("<ctrl:SchemaControl x:Name=\"SchemaControl\"/>", xaml);

        Assert.Contains("IsVisible=\"{Binding ShowNodes}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding ShowConnection}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding ShowSchema}\"", xaml);
    }

    private static string ReadSidebarXaml()
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
                "SidebarControl.axaml"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate SidebarControl.axaml from test base directory.");
    }
}
