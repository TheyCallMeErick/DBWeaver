using DBWeaver.UI.Services.Benchmark;
using System.ComponentModel;
using DBWeaver.Metadata;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels;

public class AutoJoinOverlayLocalizationAndCommandsTests
{
    [Fact]
    public void Show_Title_UsesLocalizedTemplate()
    {
        var localization = new FakeLocalizationService(new Dictionary<string, string>
        {
            ["autoJoin.titleForTable"] = "Sugestoes para {0}",
        });
        var overlay = new AutoJoinOverlayViewModel(localization);

        overlay.Show("public.orders", new[] { CreateSuggestion() });

        Assert.Equal("orders", overlay.DroppedTable);
        Assert.Equal("Sugestoes para orders", overlay.Title);
    }

    [Fact]
    public void AcceptAllCommand_AcceptsVisibleCards_AndHidesOverlay()
    {
        var overlay = new AutoJoinOverlayViewModel(new FakeLocalizationService());
        int accepted = 0;
        overlay.JoinAccepted += (_, _) => accepted++;

        overlay.Show("orders", new[] { CreateSuggestion(), CreateSuggestion() });

        overlay.AcceptAllCommand.Execute(null);

        Assert.Equal(2, accepted);
        Assert.False(overlay.IsVisible);
        Assert.All(overlay.Cards, c => Assert.False(c.IsVisible));
    }

    [Fact]
    public void JoinSuggestionCard_ConfidenceLabel_IsLocalized()
    {
        var localization = new FakeLocalizationService(new Dictionary<string, string>
        {
            ["autoJoin.joinKeyword"] = "JOIN",
            ["autoJoin.confidence.fkConstraint"] = "FK Localizada",
            ["autoJoin.confidence.fkReverse"] = "FK Reversa Localizada",
            ["autoJoin.confidence.namingMatch"] = "Nome Localizado",
            ["autoJoin.confidence.weakMatch"] = "Fraca Localizada",
        });

        var strong = new JoinSuggestionCardViewModel(CreateSuggestion(confidence: JoinConfidence.CatalogDefinedFk), localization);
        var reverse = new JoinSuggestionCardViewModel(CreateSuggestion(confidence: JoinConfidence.CatalogDefinedReverse), localization);
        var naming = new JoinSuggestionCardViewModel(CreateSuggestion(confidence: JoinConfidence.HeuristicStrong), localization);
        var weak = new JoinSuggestionCardViewModel(CreateSuggestion(confidence: JoinConfidence.HeuristicWeak), localization);

        Assert.Equal("FK Localizada", strong.ConfidenceLabel);
        Assert.Equal("FK Reversa Localizada", reverse.ConfidenceLabel);
        Assert.Equal("Nome Localizado", naming.ConfidenceLabel);
        Assert.Equal("Fraca Localizada", weak.ConfidenceLabel);
    }

    private static JoinSuggestion CreateSuggestion(JoinConfidence confidence = JoinConfidence.CatalogDefinedFk)
        => new(
            ExistingTable: "users",
            NewTable: "orders",
            JoinType: "LEFT",
            LeftColumn: "orders.user_id",
            RightColumn: "users.id",
            OnClause: "orders.user_id = users.id",
            Score: 0.95,
            Confidence: confidence,
            Rationale: "test",
            SourceFk: null
        );

    private sealed class FakeLocalizationService(Dictionary<string, string>? strings = null) : ILocalizationService
    {
        private readonly Dictionary<string, string> _strings = strings ?? new Dictionary<string, string>();
        public event PropertyChangedEventHandler? PropertyChanged;
        public string CurrentCulture => "pt-BR";
        public string CurrentLanguageLabel => "PT-BR";
        public string this[string key] => _strings.TryGetValue(key, out string? value) ? value : key;
        public bool ToggleCulture() => false;
        public bool SetCulture(string culture)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            return true;
        }
    }
}

