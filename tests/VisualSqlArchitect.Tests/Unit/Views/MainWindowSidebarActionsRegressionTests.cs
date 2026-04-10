using System.IO;
using System.Linq;
using Xunit;

namespace DBWeaver.Tests.Unit.Views;

public class MainWindowSidebarActionsRegressionTests
{
    [Fact]
    public void MainWindow_WiresSidebarActions_ToSearchAndConnectionsPanel()
    {
        string source = ReadMainWindowSources();

        Assert.Contains("sidebar.AddNodeRequested += () =>", source);
        Assert.Contains("OpenSearch();", source);

        Assert.Contains("sidebar.AddConnectionRequested += () =>", source);
        Assert.Contains("_globalModalManager.RequestConnectionManager(beginNewProfile: true, keepStartVisible: false);", source);
        Assert.Contains("private ConnectionWorkspaceModule GetConnectionModule()", source);
        Assert.Contains("GetConnectionModule().OpenManager(beginNewProfile, keepStartVisible);", source);
    }

    [Fact]
    public void MainWindow_UsesOpenConnectionsPanel_ForStartAndHeaderFlows()
    {
        string source = ReadMainWindowSources();

        Assert.Contains("private void OnStartOpenConnectionsRequested()", source);
        Assert.Contains("_globalModalManager.RequestConnectionManager(beginNewProfile: true, keepStartVisible: true);", source);
        Assert.Contains("_globalModalManager.RequestConnectionManager(beginNewProfile: false, keepStartVisible: true);", source);

        Assert.DoesNotContain("B(\"ConnectionBadgeBtn\", () =>", source);
        Assert.Contains("CurrentShell.StartMenu.OpenSavedConnectionRequested += OnStartOpenSavedConnectionRequested;", source);
        Assert.Contains("vm.ConnectionManager.IsVisible = false;", source);
        Assert.Contains("CurrentVm.ConnectionManager.IsVisible = false;", source);
    }

    [Fact]
    public void MainWindow_WiresGlobalModalManager_AsCentralModalChannel()
    {
        string source = ReadMainWindowSources();

        Assert.Contains("private readonly IGlobalModalManager _globalModalManager;", source);
        Assert.Contains("WireGlobalModalManager();", source);
        Assert.Contains("private void OnGlobalModalRequested(GlobalModalRequest request)", source);
        Assert.Contains("case GlobalModalKind.ConnectionManager:", source);
        Assert.Contains("case GlobalModalKind.Settings:", source);
    }

    [Fact]
    public void MainWindow_StartOpenFromDisk_EntersCanvasOnlyAfterFileSelection()
    {
        string source = ReadMainWindowSources();

        Assert.Contains("private async void OnStartOpenFromDiskRequested()", source);
        Assert.Contains("StorageProvider.OpenFilePickerAsync", source);
        Assert.Contains("if (selectedPath is null)", source);
        Assert.Contains("EnterCanvasMode();", source);
        Assert.Contains("OpenPathAsync(selectedPath)", source);
    }

    private static string ReadMainWindowSources()
    {
        string shellDir = FindMainWindowShellDirectory();
        string[] files = Directory.GetFiles(shellDir, "MainWindow*.cs", SearchOption.TopDirectoryOnly);

        if (files.Length == 0)
            throw new FileNotFoundException("Could not locate MainWindow*.cs files under Views/Shell.");

        return string.Join(
            "\n",
            files
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(File.ReadAllText)
        );
    }

    private static string FindMainWindowShellDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string shellDir = Path.Combine(
                dir.FullName,
                "src",
                "DBWeaver.UI",
                "Views",
                "Shell"
            );

            if (Directory.Exists(shellDir))
                return shellDir;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Views/Shell from test base directory.");
    }
}
