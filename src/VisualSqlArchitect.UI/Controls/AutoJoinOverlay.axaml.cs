using Avalonia.Controls;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

public sealed partial class AutoJoinOverlay : UserControl
{
    public AutoJoinOverlay()
    {
        InitializeComponent();

        // Header-level buttons
        Button? acceptAll = this.FindControl<Button>("AcceptAllBtn");
        Button? dismissAll = this.FindControl<Button>("DismissAllBtn");

        if (acceptAll is not null)
            acceptAll.Click += (_, _) => (DataContext as AutoJoinOverlayViewModel)?.AcceptAll();
        if (dismissAll is not null)
            dismissAll.Click += (_, _) => (DataContext as AutoJoinOverlayViewModel)?.Dismiss();

        // Per-card buttons are wired in the ItemsControl template via code-behind
        // because AXAML Command bindings on DataTemplate items require compiled bindings
        DataContextChanged += (_, _) => WireCardButtons();
    }

    private static void WireCardButtons()
    {
        // Re-wire when the DataContext (VM) is swapped.
        // Card-level Accept/Dismiss are handled inside JoinSuggestionCardViewModel directly
        // when the user clicks the button — the card's DataContext is the card VM.
    }
}
