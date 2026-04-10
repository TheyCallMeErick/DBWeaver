using System.Collections.ObjectModel;
using System.ComponentModel;
using DBWeaver.Metadata;
using DBWeaver.UI.Services.Localization;

namespace DBWeaver.UI.ViewModels;

// ─── Overlay VM ──────────────────────────────────────────────────────────────

/// <summary>
/// Drives the floating "Auto-Join Suggestions" banner that appears when a table
/// is dragged onto the canvas and the metadata engine detects FK relationships.
///
/// The canvas shows this as a floating panel with Accept/Dismiss buttons per card.
/// High-confidence suggestions (FK catalog) are pre-selected.
/// </summary>
public sealed class AutoJoinOverlayViewModel : ViewModelBase, IDisposable
{
    private readonly ILocalizationService _localization;
    private readonly PropertyChangedEventHandler _localizationChangedHandler;
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
    public string Title =>
        string.Format(_localization["autoJoin.titleForTable"], DroppedTable);

    public RelayCommand AcceptAllCommand { get; }
    public RelayCommand DismissCommand { get; }

    public AutoJoinOverlayViewModel(ILocalizationService? localization = null)
    {
        _localization = localization ?? LocalizationService.Instance;
        _localizationChangedHandler = (_, e) =>
        {
            if (e.PropertyName is "" or "Item[]" or nameof(ILocalizationService.CurrentCulture))
            {
                RaisePropertyChanged(nameof(Title));
                foreach (JoinSuggestionCardViewModel card in Cards.ToList())
                    card.RefreshLocalization();
            }
        };
        _localization.PropertyChanged += _localizationChangedHandler;

        AcceptAllCommand = new RelayCommand(AcceptAll);
        DismissCommand = new RelayCommand(Dismiss);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised when the user accepts a suggestion. The canvas wires the nodes.</summary>
    public event EventHandler<JoinSuggestion>? JoinAccepted;

    // ── Show / hide ───────────────────────────────────────────────────────────

    /// <summary>
    /// Clears all cards and unsubscribes their event handlers to prevent memory leaks.
    /// Should be called before creating new cards or when the overlay is being disposed.
    /// </summary>
    private void ClearCardsAndUnsubscribe()
    {
        // Unsubscribe from all card events before clearing the collection
        // This prevents memory leaks when cards are removed from the UI
        foreach (JoinSuggestionCardViewModel card in Cards.ToList())
        {
            card.Accepted -= OnCardAccepted;
            card.Dismissed -= OnCardDismissed;
            card.Dispose();
        }

        // Now clear the collection
        Cards.Clear();
    }

    public void Show(string tableName, IReadOnlyList<JoinSuggestion> suggestions)
    {
        DroppedTable = tableName.Split('.').Last();
        AcceptedCount = 0;

        // Clear old cards and unsubscribe their handlers before adding new ones
        ClearCardsAndUnsubscribe();

        foreach (JoinSuggestion s in suggestions)
        {
            var card = new JoinSuggestionCardViewModel(s, _localization);
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
        foreach (JoinSuggestionCardViewModel c in Cards.ToList())
            c.Dismiss();
        IsVisible = false;
    }

    public void AcceptAll()
    {
        foreach (JoinSuggestionCardViewModel? c in Cards.Where(c => c.IsVisible).ToList())
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

    /// <summary>
    /// Disposes the ViewModel by clearing all cards and unsubscribing event handlers.
    /// Called when the parent CanvasViewModel is disposed.
    /// </summary>
    public void Dispose()
    {
        ClearCardsAndUnsubscribe();
        _localization.PropertyChanged -= _localizationChangedHandler;
    }
}
