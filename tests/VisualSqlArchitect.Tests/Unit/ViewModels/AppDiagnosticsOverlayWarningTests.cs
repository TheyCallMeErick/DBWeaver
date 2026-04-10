using DBWeaver.UI.Services.Benchmark;

using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class AppDiagnosticsOverlayWarningTests
{
    [Fact]
    public void AddWarning_OpenPanelTrue_AppendsWarningAndShowsOverlay()
    {
        var canvas = new CanvasViewModel();
        var vm = canvas.Diagnostics;

        vm.AddWarning(
            area: "Canvas Migration",
            message: "Open: File migrated from schema v2 to v3.",
            recommendation: "Re-save the canvas.",
            openPanel: true
        );

        Assert.True(vm.IsVisible);
        Assert.Contains(
            vm.SnapshotEntries(),
            e =>
                e.Name == "Canvas Migration"
                && e.Status == DiagnosticStatus.Warning
                && e.Details.Contains("migrated", StringComparison.OrdinalIgnoreCase)
        );
    }
}

