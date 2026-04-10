using System.Collections.ObjectModel;
using DBWeaver.Core;
using DBWeaver.UI.Services.Localization;

namespace DBWeaver.UI.ViewModels;

public sealed class SqlEditorTabManagerViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private int _activeTabIndex;

    public SqlEditorTabManagerViewModel(ILocalizationService? localization = null)
    {
        _localization = localization ?? LocalizationService.Instance;
    }

    public ObservableCollection<SqlEditorTabState> Tabs { get; } = [];

    public int ActiveTabIndex
    {
        get => _activeTabIndex;
        private set => Set(ref _activeTabIndex, value);
    }

    public void Initialize(DatabaseProvider provider, string? connectionProfileId = null)
    {
        Tabs.Clear();
        Tabs.Add(CreateTab(1, provider, connectionProfileId, _localization));
        ActiveTabIndex = 0;
    }

    public SqlEditorTabState AddNewTab(DatabaseProvider? provider = null, string? connectionProfileId = null)
    {
        DatabaseProvider resolvedProvider = provider ?? GetTab(ActiveTabIndex)?.Provider ?? DatabaseProvider.Postgres;
        string? resolvedConnection = connectionProfileId ?? GetTab(ActiveTabIndex)?.ConnectionProfileId;

        SqlEditorTabState tab = CreateTab(Tabs.Count + 1, resolvedProvider, resolvedConnection, _localization);
        Tabs.Add(tab);
        ActiveTabIndex = Tabs.Count - 1;
        return tab;
    }

    public SqlEditorTabState? GetTab(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= Tabs.Count)
            return null;

        return Tabs[tabIndex];
    }

    public SqlEditorTabState GetActiveTab()
    {
        SqlEditorTabState? active = GetTab(ActiveTabIndex);
        if (active is not null)
            return active;

        Initialize(DatabaseProvider.Postgres);
        return Tabs[0];
    }

    public bool TryActivate(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= Tabs.Count || tabIndex == ActiveTabIndex)
            return false;

        ActiveTabIndex = tabIndex;
        return true;
    }

    public int CloseTab(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= Tabs.Count || Tabs.Count <= 1)
            return ActiveTabIndex;

        Tabs.RemoveAt(tabIndex);

        int newActive = ActiveTabIndex;
        if (tabIndex < ActiveTabIndex)
            newActive = ActiveTabIndex - 1;
        else if (tabIndex == ActiveTabIndex)
            newActive = Math.Min(tabIndex, Tabs.Count - 1);

        ActiveTabIndex = newActive;
        return newActive;
    }

    public void ReceiveFromCanvas(string sql, DatabaseProvider provider)
    {
        SqlEditorTabState activeTab = GetActiveTab();
        SqlEditorTabState target = string.IsNullOrWhiteSpace(activeTab.SqlText)
            ? activeTab
            : AddNewTab(provider, activeTab.ConnectionProfileId);

        target.SqlText = sql;
        target.Provider = provider;
        target.IsDirty = false;
    }

    private static SqlEditorTabState CreateTab(
        int number,
        DatabaseProvider provider,
        string? connectionProfileId,
        ILocalizationService localization)
    {
        return new SqlEditorTabState
        {
            Id = Guid.NewGuid().ToString("N"),
            FallbackTitle = string.Format(
                L(localization, "sqlEditor.tab.scriptTitle", "Script {0}"),
                number),
            Provider = provider,
            ConnectionProfileId = connectionProfileId,
        };
    }

    private static string L(ILocalizationService localization, string key, string fallback)
    {
        string value = localization[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
