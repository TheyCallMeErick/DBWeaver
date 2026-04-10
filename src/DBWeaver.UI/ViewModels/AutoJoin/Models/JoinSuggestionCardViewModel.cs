using System.Windows.Input;
using Avalonia.Media;
using DBWeaver.Metadata;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.ViewModels;

public sealed class JoinSuggestionCardViewModel : ViewModelBase, IDisposable
{
    private readonly ILocalizationService _localization;
    private bool _isAccepted;
    private bool _isDismissed;

    public JoinSuggestion Suggestion { get; }

    public string TablePair => $"{Suggestion.ExistingTable}  ↔  {Suggestion.NewTable}";
    public string OnClause => Suggestion.OnClause;
    public string JoinType => $"{Suggestion.JoinType} {_localization["autoJoin.joinKeyword"]}";
    public string Rationale => Suggestion.Rationale;
    public string ScoreLabel => $"{Suggestion.Score * 100:F0}%";
    public bool IsCatalogFk => Suggestion.Confidence >= JoinConfidence.CatalogDefinedFk;
    public double ScoreBarWidth => Math.Max(4, Suggestion.Score * 340);

    public Color ConfidenceColor =>
        Suggestion.Confidence switch
        {
            >= JoinConfidence.CatalogDefinedFk => Color.Parse(UiColorConstants.C_4ADE80),
            >= JoinConfidence.CatalogDefinedReverse => Color.Parse(UiColorConstants.C_60A5FA),
            >= JoinConfidence.HeuristicStrong => Color.Parse(UiColorConstants.C_FBBF24),
            _ => Color.Parse(UiColorConstants.C_94A3B8),
        };

    public SolidColorBrush ConfidenceBrush => new(ConfidenceColor);

    public string ConfidenceLabel =>
        Suggestion.Confidence switch
        {
            >= JoinConfidence.CatalogDefinedFk => _localization["autoJoin.confidence.fkConstraint"],
            >= JoinConfidence.CatalogDefinedReverse => _localization["autoJoin.confidence.fkReverse"],
            >= JoinConfidence.HeuristicStrong => _localization["autoJoin.confidence.namingMatch"],
            _ => _localization["autoJoin.confidence.weakMatch"],
        };

    public bool IsAccepted
    {
        get => _isAccepted;
        private set => Set(ref _isAccepted, value);
    }

    public bool IsDismissed
    {
        get => _isDismissed;
        private set => Set(ref _isDismissed, value);
    }

    public bool IsVisible => !IsAccepted && !IsDismissed;

    public ICommand AcceptCommand { get; }
    public ICommand DismissCommand { get; }

    public event EventHandler<JoinSuggestion>? Accepted;
    public event EventHandler<JoinSuggestion>? Dismissed;

    public JoinSuggestionCardViewModel(JoinSuggestion suggestion, ILocalizationService? localization = null)
    {
        Suggestion = suggestion;
        _localization = localization ?? LocalizationService.Instance;
        _localization.PropertyChanged += OnLocalizationChanged;
        AcceptCommand = new RelayCommand(Accept);
        DismissCommand = new RelayCommand(Dismiss);
    }

    public void Accept()
    {
        IsAccepted = true;
        RaisePropertyChanged(nameof(IsVisible));
        Accepted?.Invoke(this, Suggestion);
    }

    public void Dismiss()
    {
        IsDismissed = true;
        RaisePropertyChanged(nameof(IsVisible));
        Dismissed?.Invoke(this, Suggestion);
    }

    public void RefreshLocalization()
    {
        RaisePropertyChanged(nameof(JoinType));
        RaisePropertyChanged(nameof(ConfidenceLabel));
    }

    private void OnLocalizationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "" or "Item[]" or nameof(ILocalizationService.CurrentCulture))
            RefreshLocalization();
    }

    public void Dispose()
    {
        _localization.PropertyChanged -= OnLocalizationChanged;
    }
}
