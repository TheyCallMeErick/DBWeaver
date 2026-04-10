using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class DataPreviewTimeoutTests
{
    [Fact]
    public void ShowLoading_WithTimeout_IncludesTimeoutInStatus()
    {
        var vm = new DataPreviewViewModel();

        vm.ShowLoading("SELECT 1", timeoutSeconds: 300);
        vm.UpdateElapsed(1_000);

        Assert.Contains("timeout: 300s", vm.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.IsNearTimeout);
    }

    [Fact]
    public void UpdateElapsed_After80PercentTimeout_ShowsSlowQueryWarning()
    {
        var vm = new DataPreviewViewModel();

        vm.ShowLoading("SELECT 1", timeoutSeconds: 10);
        vm.UpdateElapsed(8_200);

        Assert.True(vm.IsNearTimeout);
        Assert.Contains("timeout", vm.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2s", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }
}


