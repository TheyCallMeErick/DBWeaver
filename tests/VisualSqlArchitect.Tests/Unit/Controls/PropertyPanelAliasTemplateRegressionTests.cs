using System.IO;
using Xunit;

namespace DBWeaver.Tests.Unit.Controls;

public class PropertyPanelAliasTemplateRegressionTests
{
    [Fact]
    public void AliasSection_BindsVisibilityToShowAliasEditor()
    {
        string xaml = ReadPropertyPanelXaml();

        Assert.Contains("IsVisible=\"{Binding ShowAliasEditor}\"", xaml);
        Assert.Contains("Text=\"{Binding NodeAliasLabel}\"", xaml);
        Assert.Contains("Text=\"{Binding NodeAlias, Mode=TwoWay}\"", xaml);
    }

    private static string ReadPropertyPanelXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "DBWeaver.UI",
                "Controls",
                "PropertyPanel",
                "PropertyPanelControl.axaml"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate PropertyPanelControl.axaml from test base directory.");
    }
}
