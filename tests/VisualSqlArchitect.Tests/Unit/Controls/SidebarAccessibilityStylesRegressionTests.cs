using System.IO;
using Xunit;

namespace DBWeaver.Tests.Unit.Controls;

public class SidebarAccessibilityStylesRegressionTests
{
    [Fact]
    public void AppStyles_DefinesFocusVisibleStyles_ForSidebarInputsAndToggles()
    {
        string styles = ReadAppStyles();

        Assert.Contains("TextBox.sidebar-input:focus-visible /template/ Border#PART_BorderElement", styles);
        Assert.Contains("ToggleButton:focus-visible", styles);
        Assert.Contains("Button:disabled", styles);
        Assert.Contains("ToggleButton:disabled", styles);
    }

    [Fact]
    public void SidebarTemplate_DefinesFocusVisibleStyle_ForTabButtons()
    {
        string xaml = ReadSidebarXaml();

        Assert.Contains("Style Selector=\"Button.tab-button:focus-visible\"", xaml);
        Assert.Contains("Property=\"Border.BorderBrush\" Value=\"{StaticResource BorderActiveBrush}\"", xaml);
        Assert.Contains("Style Selector=\"Button.tab-button:disabled\"", xaml);
    }

    private static string ReadAppStyles()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "src", "DBWeaver.UI", "Assets", "Themes", "AppStyles.axaml");
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate AppStyles.axaml from test base directory.");
    }

    private static string ReadSidebarXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "src", "DBWeaver.UI", "Controls", "SidebarLeft", "SidebarControl.axaml");
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate SidebarControl.axaml from test base directory.");
    }
}
