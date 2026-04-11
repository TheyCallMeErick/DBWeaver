using System.IO;

namespace DBWeaver.Tests.Unit.Controls;

public sealed class SqlEditorTabBarTemplateRegressionTests
{
    [Fact]
    public void SqlEditorTabBarTemplate_BindsTabsAndCommands()
    {
        string xaml = ReadXaml();

        Assert.Contains("x:DataType=\"vm:SqlEditorViewModel\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding EditorTabs}\"", xaml);
        Assert.Contains("SelectedIndex=\"{Binding ActiveEditorTabIndex}\"", xaml);
        Assert.Contains("Command=\"{Binding NewTabCommand}\"", xaml);
        Assert.Contains("Command=\"{Binding CloseCommand}\"", xaml);
        Assert.Contains("CommandParameter=\"{Binding Id}\"", xaml);
        Assert.Contains("HorizontalScrollBarVisibility=\"Auto\"", xaml);
        Assert.DoesNotContain("ItemsSource=\"{Binding AvailableProviders}\"", xaml);
        Assert.DoesNotContain("SelectedItem=\"{Binding ActiveTabProvider}\"", xaml);
        Assert.DoesNotContain("ItemsSource=\"{Binding AvailableConnectionProfiles}\"", xaml);
        Assert.DoesNotContain("SelectedItem=\"{Binding ActiveTabConnectionProfile}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding HasManyTabsWarning}\"", xaml);
        Assert.Contains("Text=\"{Binding ManyTabsWarningText}\"", xaml);
        Assert.Contains("Text=\"●\"", xaml);
        Assert.Contains("IsVisible=\"{Binding IsDirty}\"", xaml);
        Assert.DoesNotContain("x:CompileBindings=\"False\"", xaml);
    }

    private static string ReadXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "DBWeaver.UI",
                "Controls",
                "SqlEditor",
                "SqlEditorTabBar.axaml");

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate SqlEditorTabBar.axaml from test base directory.");
    }
}
