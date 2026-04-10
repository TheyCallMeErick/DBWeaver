using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorTabCloseWorkflowService
{
    private readonly ILocalizationService _localization;
    private string? _pendingCloseTabId;

    public SqlEditorTabCloseWorkflowService(ILocalizationService? localization = null)
    {
        _localization = localization ?? LocalizationService.Instance;
    }

    public bool HasPendingConfirmation => !string.IsNullOrWhiteSpace(_pendingCloseTabId);

    public string PendingMessage =>
        HasPendingConfirmation
            ? L("sqlEditor.tab.closePending", "Unsaved changes detected. Confirm tab close.")
            : L("sqlEditor.tab.noPendingClose", "No tab close pending.");

    public SqlEditorTabCloseOutcome RequestClose(SqlEditorTabManagerViewModel tabs, string? tabId)
    {
        if (string.IsNullOrWhiteSpace(tabId) || tabs.Tabs.Count <= 1)
        {
            return new SqlEditorTabCloseOutcome
            {
                Action = SqlEditorTabCloseAction.None,
            };
        }

        int index = FindTabIndexById(tabs, tabId);
        if (index < 0)
        {
            return new SqlEditorTabCloseOutcome
            {
                Action = SqlEditorTabCloseAction.None,
            };
        }

        SqlEditorTabState tab = tabs.Tabs[index];
        if (tab.IsDirty)
        {
            _pendingCloseTabId = tab.Id;
            return new SqlEditorTabCloseOutcome
            {
                Action = SqlEditorTabCloseAction.ConfirmationRequired,
                StatusText = L("sqlEditor.tab.closeRequiresConfirmation", "Tab close requires confirmation."),
                DetailText = L("sqlEditor.tab.unsavedDetail", "This tab has unsaved changes."),
                HasError = false,
            };
        }

        tabs.CloseTab(index);
        _pendingCloseTabId = null;
        return new SqlEditorTabCloseOutcome
        {
            Action = SqlEditorTabCloseAction.Closed,
        };
    }

    public SqlEditorTabCloseOutcome ConfirmClose(SqlEditorTabManagerViewModel tabs)
    {
        if (string.IsNullOrWhiteSpace(_pendingCloseTabId))
        {
            return new SqlEditorTabCloseOutcome
            {
                Action = SqlEditorTabCloseAction.None,
            };
        }

        int index = FindTabIndexById(tabs, _pendingCloseTabId);
        if (index >= 0 && tabs.Tabs.Count > 1)
            tabs.CloseTab(index);

        _pendingCloseTabId = null;
        return new SqlEditorTabCloseOutcome
        {
            Action = SqlEditorTabCloseAction.Closed,
            StatusText = L("sqlEditor.tab.closed", "Tab closed."),
            DetailText = null,
            HasError = false,
        };
    }

    public SqlEditorTabCloseOutcome CancelClose()
    {
        if (!HasPendingConfirmation)
        {
            return new SqlEditorTabCloseOutcome
            {
                Action = SqlEditorTabCloseAction.None,
            };
        }

        _pendingCloseTabId = null;
        return new SqlEditorTabCloseOutcome
        {
            Action = SqlEditorTabCloseAction.None,
            StatusText = L("sqlEditor.tab.closeCanceled", "Tab close canceled."),
            DetailText = L("sqlEditor.tab.closeCanceledDetail", "Unsaved tab kept open."),
            HasError = false,
        };
    }

    public bool CanCloseTab(SqlEditorTabManagerViewModel tabs, string? tabId)
    {
        if (string.IsNullOrWhiteSpace(tabId) || tabs.Tabs.Count <= 1)
            return false;

        return FindTabIndexById(tabs, tabId) >= 0;
    }

    private static int FindTabIndexById(SqlEditorTabManagerViewModel tabs, string tabId)
    {
        for (int i = 0; i < tabs.Tabs.Count; i++)
        {
            if (string.Equals(tabs.Tabs[i].Id, tabId, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private string L(string key, string fallback)
    {
        string value = _localization[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}

