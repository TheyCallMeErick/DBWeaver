using System.IO;
using Xunit;

namespace DBWeaver.Tests.Unit.Controls;

public class ConnectionTabControlTemplateRegressionTests
{
    [Fact]
    public void ConnectionTemplate_ContainsDatabaseConnectionCard()
    {
        string xaml = ReadConnectionXaml();

        Assert.Contains("DatabaseConnectionCard", xaml);
    }

    [Fact]
    public void ConnectionTemplate_ContainsSchemaControl()
    {
        string xaml = ReadConnectionXaml();

        Assert.Contains("ctrl:SchemaControl", xaml);
        Assert.Contains("DataContext=\"{Binding Canvas.Schema}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding ActiveProfileId, Converter={x:Static StringConverters.IsNotNullOrEmpty}}\"", xaml);
    }

    [Fact]
    public void ConnectionTemplate_SectionHeadersUseUppercaseTealCaption()
    {
        string xaml = ReadConnectionXaml();

        Assert.Contains("section-header-teal", xaml);
    }

    [Fact]
    public void ConnectionTemplate_UsesPrimaryClassForNewConnectionCta()
    {
        string xaml = ReadConnectionXaml();

        Assert.Contains("Classes=\"primary\"", xaml);
        Assert.Contains("Command=\"{Binding ConnectOrOpenManagerCommand}\"", xaml);
    }

    [Fact]
    public void ConnectionTemplate_RemovesSavedProfilesSection()
    {
        string xaml = ReadConnectionXaml();

        Assert.DoesNotContain("PERFIS SALVOS", xaml);
        Assert.DoesNotContain("ItemsSource=\"{Binding Profiles}\"", xaml);
    }

    [Fact]
    public void ConnectionTemplate_NoConnectionStateHasConnectButton()
    {
        string xaml = ReadConnectionXaml();

        Assert.Contains("IsVisible=\"{Binding ActiveProfileId, Converter={x:Static StringConverters.IsNullOrEmpty}}\"", xaml);
        Assert.Contains("Command=\"{Binding ConnectOrOpenManagerCommand}\"", xaml);
    }

    [Fact]
    public void ConnectionTemplate_ShowsLoadingStateWhileConnecting()
    {
        string xaml = ReadConnectionXaml();

        Assert.Contains("IsVisible=\"{Binding IsConnecting}\"", xaml);
        Assert.Contains("Conectando...", xaml);
        Assert.Contains("IsEnabled=\"{Binding IsConnecting, Converter={x:Static BoolConverters.Not}}\"", xaml);
        Assert.Contains("IsReloading=\"{Binding IsBusy}\"", xaml);
    }

    [Fact]
    public void ConnectionTemplate_UsesStretchLayoutForFullHeightSidebar()
    {
        string xaml = ReadConnectionXaml();

        Assert.Contains("VerticalAlignment=\"Stretch\"", xaml);
        Assert.Contains("RowDefinitions=\"Auto,*\"", xaml);
    }

    private static string ReadConnectionXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName, "src", "DBWeaver.UI",
                "Controls", "SidebarLeft", "ConnectionTabControl.axaml");

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate ConnectionTabControl.axaml.");
    }
}
