using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using System.ComponentModel;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class CanvasAutoJoinMessagePresenterTests
{
    [Fact]
    public void MultipleCandidatesFound_FormatsSuggestionCount()
    {
        var localization = new FakeLocalizationService(new Dictionary<string, string>
        {
            ["autoJoin.multipleCandidatesTitle"] = "Multiple",
            ["autoJoin.multipleCandidatesDetails"] = "Found {0} candidates",
        });
        var presenter = new CanvasAutoJoinMessagePresenter(localization);

        AutoJoinToastMessage msg = presenter.MultipleCandidatesFound(3);

        Assert.Equal("Multiple", msg.Message);
        Assert.Equal("Found 3 candidates", msg.Details);
    }

    [Fact]
    public void ManualJoinCreated_ContainsJoinEqualityDetails()
    {
        var presenter = new CanvasAutoJoinMessagePresenter(new FakeLocalizationService(new Dictionary<string, string>
        {
            ["autoJoin.manualJoinCreatedTitle"] = "Manual join created",
        }));

        AutoJoinToastMessage msg = presenter.ManualJoinCreated("orders.customer_id", "customers.id");

        Assert.Equal("Manual join created", msg.Message);
        Assert.Equal("orders.customer_id = customers.id", msg.Details);
    }

    [Fact]
    public void SuggestionsUnavailable_UsesFallbackWhenLocalizationKeyMissing()
    {
        var presenter = new CanvasAutoJoinMessagePresenter(new FakeLocalizationService());

        AutoJoinToastMessage msg = presenter.SuggestionsUnavailable();

        Assert.Equal("Suggestion details unavailable", msg.Message);
        Assert.Equal("Could not resolve tables in the current canvas to prefill the join modal.", msg.Details);
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


