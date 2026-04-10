using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using System.ComponentModel;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class CanvasAutoJoinNotifierTests
{
    [Fact]
    public void ShowAutoJoinApplied_SetsSuccessToast()
    {
        var toasts = new ToastCenterViewModel();
        var presenter = new CanvasAutoJoinMessagePresenter(new FakeLocalizationService(new Dictionary<string, string>
        {
            ["autoJoin.appliedTitle"] = "Auto-join applied",
        }));
        var notifier = new CanvasAutoJoinNotifier(toasts, presenter);

        notifier.ShowAutoJoinApplied("orders.customer_id = customers.id");

        Assert.True(toasts.IsVisible);
        Assert.Equal(ToastSeverity.Success, toasts.Severity);
        Assert.Equal("Auto-join applied", toasts.Message);
        Assert.Equal("orders.customer_id = customers.id", toasts.Details);
    }

    [Fact]
    public void ShowSuggestionsFound_UsesDetailsAction()
    {
        var toasts = new ToastCenterViewModel();
        var presenter = new CanvasAutoJoinMessagePresenter(new FakeLocalizationService(new Dictionary<string, string>
        {
            ["autoJoin.suggestionsFoundTitle"] = "Suggestions",
            ["autoJoin.suggestionsFoundDetails"] = "Found {0} suggestions",
        }));
        var notifier = new CanvasAutoJoinNotifier(toasts, presenter);

        bool detailsInvoked = false;
        Action onDetails = () => detailsInvoked = true;

        notifier.ShowSuggestionsFound(3, onDetails);

        Assert.True(toasts.HasDetailsAction);
        toasts.ShowDetailsCommand.Execute(null);
        Assert.True(detailsInvoked);
    }

    [Fact]
    public void ShowManualJoinFailed_SetsWarningToast()
    {
        var toasts = new ToastCenterViewModel();
        var presenter = new CanvasAutoJoinMessagePresenter(new FakeLocalizationService(new Dictionary<string, string>
        {
            ["autoJoin.manualJoinFailedTitle"] = "Manual join failed",
            ["autoJoin.manualJoinFailedDetails"] = "Fix columns",
        }));
        var notifier = new CanvasAutoJoinNotifier(toasts, presenter);

        notifier.ShowManualJoinFailed();

        Assert.True(toasts.IsVisible);
        Assert.Equal(ToastSeverity.Warning, toasts.Severity);
        Assert.Equal("Manual join failed", toasts.Message);
        Assert.Equal("Fix columns", toasts.Details);
    }
    private sealed class FakeLocalizationService(Dictionary<string, string>? values = null) : ILocalizationService
    {
        private readonly Dictionary<string, string> _values = values ?? [];

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public string CurrentCulture => "en-US";
        public string CurrentLanguageLabel => "English";
        public string this[string key] => _values.TryGetValue(key, out string? value) ? value : key;
        public bool ToggleCulture() => false;
        public bool SetCulture(string culture) => false;
    }
}


