using DBWeaver.UI.Services.QueryPreview.Models;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services.LiveSqlBar;

public sealed class LiveSqlDiagnosticItem
{
    private readonly PreviewDiagnostic _diagnostic;

    public LiveSqlDiagnosticItem(
        PreviewDiagnostic diagnostic,
        Action<PreviewDiagnostic> focusAction,
        Action<PreviewDiagnostic>? quickAction = null,
        string? quickActionLabel = null)
    {
        _diagnostic = diagnostic;

        FocusCommand = new RelayCommand(
            () => focusAction(_diagnostic),
            () => !string.IsNullOrWhiteSpace(_diagnostic.NodeId)
        );

        QuickActionLabel = quickActionLabel;
        QuickActionCommand = new RelayCommand(
            () =>
            {
                focusAction(_diagnostic);
                quickAction?.Invoke(_diagnostic);
            },
            () => quickAction is not null
        );
    }

    public string Message => _diagnostic.Message;

    public RelayCommand FocusCommand { get; }

    public string? QuickActionLabel { get; }

    public bool HasQuickAction => !string.IsNullOrWhiteSpace(QuickActionLabel);

    public RelayCommand QuickActionCommand { get; }
}


