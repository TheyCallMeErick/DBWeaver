using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media;
using VisualSqlArchitect.Metadata;

namespace VisualSqlArchitect.UI.ViewModels;

// ─── Single suggestion card ───────────────────────────────────────────────────

public sealed class JoinSuggestionCardViewModel : ViewModelBase
{
    private bool _isAccepted;
    private bool _isDismissed;

    public JoinSuggestion Suggestion { get; }

    public string TablePair => $"{Suggestion.ExistingTable}  ↔  {Suggestion.NewTable}";
    public string OnClause => Suggestion.OnClause;
    public string JoinType => Suggestion.JoinType + " JOIN";
    public string Rationale => Suggestion.Rationale;
    public string ScoreLabel => $"{Suggestion.Score * 100:F0}%";
    public bool IsCatalogFk => Suggestion.Confidence >= JoinConfidence.CatalogDefinedFk;

    /// <summary>Width in pixels for the confidence bar (max 340px card inner width).</summary>
    public double ScoreBarWidth => Math.Max(4, Suggestion.Score * 340);

    public Color ConfidenceColor =>
        Suggestion.Confidence switch
        {
            >= JoinConfidence.CatalogDefinedFk => Color.Parse("#4ADE80"),
            >= JoinConfidence.CatalogDefinedReverse => Color.Parse("#60A5FA"),
            >= JoinConfidence.HeuristicStrong => Color.Parse("#FBBF24"),
            _ => Color.Parse("#94A3B8"),
        };

    public SolidColorBrush ConfidenceBrush => new(ConfidenceColor);

    public string ConfidenceLabel =>
        Suggestion.Confidence switch
        {
            >= JoinConfidence.CatalogDefinedFk => "FK Constraint",
            >= JoinConfidence.CatalogDefinedReverse => "FK (Reverse)",
            >= JoinConfidence.HeuristicStrong => "Naming Match",
            _ => "Weak Match",
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

    public JoinSuggestionCardViewModel(JoinSuggestion suggestion)
    {
        Suggestion = suggestion;
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
}

// ─── Overlay VM ──────────────────────────────────────────────────────────────

/// <summary>
/// Drives the floating "Auto-Join Suggestions" banner that appears when a table
/// is dragged onto the canvas and the metadata engine detects FK relationships.
///
/// The canvas shows this as a floating panel with Accept/Dismiss buttons per card.
/// High-confidence suggestions (FK catalog) are pre-selected.
/// </summary>
public sealed class AutoJoinOverlayViewModel : ViewModelBase
{
    private bool _isVisible;
    private string _droppedTable = string.Empty;
    private int _acceptedCount;

    public ObservableCollection<JoinSuggestionCardViewModel> Cards { get; } = [];

    public bool IsVisible
    {
        get => _isVisible;
        private set => Set(ref _isVisible, value);
    }

    public string DroppedTable
    {
        get => _droppedTable;
        private set => Set(ref _droppedTable, value);
    }

    public int AcceptedCount
    {
        get => _acceptedCount;
        private set => Set(ref _acceptedCount, value);
    }

    public bool HasCards => Cards.Any(c => c.IsVisible);
    public string Title => $"Auto-Join suggestions for {DroppedTable}";

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised when the user accepts a suggestion. The canvas wires the nodes.</summary>
    public event EventHandler<JoinSuggestion>? JoinAccepted;

    // ── Show / hide ───────────────────────────────────────────────────────────

    public void Show(string tableName, IReadOnlyList<JoinSuggestion> suggestions)
    {
        DroppedTable = tableName.Split('.').Last();
        AcceptedCount = 0;
        Cards.Clear();

        foreach (JoinSuggestion s in suggestions)
        {
            var card = new JoinSuggestionCardViewModel(s);
            card.Accepted += OnCardAccepted;
            card.Dismissed += OnCardDismissed;
            Cards.Add(card);
        }

        IsVisible = Cards.Count > 0;
        RaisePropertyChanged(nameof(Title));
        RaisePropertyChanged(nameof(HasCards));
    }

    public void Dismiss()
    {
        foreach (JoinSuggestionCardViewModel c in Cards)
            c.Dismiss();
        IsVisible = false;
    }

    public void AcceptAll()
    {
        foreach (JoinSuggestionCardViewModel? c in Cards.Where(c => c.IsVisible))
            c.Accept();
        CheckClose();
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private void OnCardAccepted(object? sender, JoinSuggestion s)
    {
        AcceptedCount++;
        JoinAccepted?.Invoke(this, s);
        CheckClose();
    }

    private void OnCardDismissed(object? _, JoinSuggestion __)
    {
        CheckClose();
        RaisePropertyChanged(nameof(HasCards));
    }

    private void CheckClose()
    {
        if (!Cards.Any(c => c.IsVisible))
            IsVisible = false;
        RaisePropertyChanged(nameof(HasCards));
    }
}
