using DBWeaver.UI.Services.ConnectionManager;
using DBWeaver.UI.Services.Benchmark;
using System.Reflection;
using DBWeaver.Core;
using DBWeaver.Metadata;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels;

public class ConnectionManagerSoftLockTests
{
    [Fact]
    public void Disconnect_WhenConnecting_ResetsIsConnectingImmediately()
    {
        var vm = new ConnectionManagerViewModel
        {
            IsConnecting = true,
        };
        vm.ActiveProfileId = "active";

        vm.DisconnectCommand.Execute(null);

        Assert.False(vm.IsConnecting);
        Assert.True(vm.IsNotConnecting);
        Assert.Null(vm.ActiveProfileId);
    }

    [Fact]
    public void CloseClearCanvasPrompt_WhenDismissed_AddsDiagnosticsWarning()
    {
        var vm = new ConnectionManagerViewModel();
        var canvas = new CanvasViewModel();
        vm.Canvas = canvas;

        int before = canvas.Diagnostics.SnapshotEntries().Count;

        InvokeOpenPrompt(vm);
        vm.CloseClearCanvasPromptCommand.Execute(null);

        var afterEntries = canvas.Diagnostics.SnapshotEntries();
        string expectedArea = LocalizationService.Instance["diagnostics.area.connection"];
        Assert.True(afterEntries.Count > before);
        Assert.Contains(afterEntries, e => e.Name.Contains(expectedArea, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void KeepCanvasAfterConnect_DoesNotAddDismissWarning()
    {
        var vm = new ConnectionManagerViewModel();
        var canvas = new CanvasViewModel();
        vm.Canvas = canvas;

        int before = canvas.Diagnostics.SnapshotEntries().Count;

        InvokeOpenPrompt(vm);
        MethodInfo keep = typeof(ConnectionManagerViewModel)
            .GetMethod("KeepCanvasAfterConnect", BindingFlags.Instance | BindingFlags.NonPublic)!;
        keep.Invoke(vm, null);

        int after = canvas.Diagnostics.SnapshotEntries().Count;
        Assert.Equal(before, after);
    }

    private static void InvokeOpenPrompt(ConnectionManagerViewModel vm)
    {
        MethodInfo open = typeof(ConnectionManagerViewModel)
            .GetMethod("OpenClearCanvasPrompt", BindingFlags.Instance | BindingFlags.NonPublic)!;
        open.Invoke(vm, [BuildMetadata(), BuildConfig()]);
        Assert.True(vm.IsClearCanvasPromptVisible);
    }

    private static ConnectionConfig BuildConfig() =>
        new(
            DatabaseProvider.Postgres,
            "localhost",
            5432,
            "db",
            "u",
            "p"
        );

    private static DbMetadata BuildMetadata() =>
        new(
            DatabaseName: "db",
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "16",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [],
            AllForeignKeys: []
        );
}


