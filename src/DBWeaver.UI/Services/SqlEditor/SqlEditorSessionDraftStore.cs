using DBWeaver.UI.Services.Settings;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorSessionDraftStore : ISqlEditorSessionDraftStore
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    public IReadOnlyList<SqlEditorSessionDraftEntry> LoadDrafts()
    {
        IReadOnlyList<SqlEditorSessionDraftEntry> persisted = AppSettingsStore.LoadSqlEditorSessionDrafts();
        if (persisted.Count == 0)
            return [];

        DateTimeOffset cutoff = DateTimeOffset.UtcNow.Subtract(Retention);
        List<SqlEditorSessionDraftEntry> valid = persisted
            .Where(static draft => !string.IsNullOrWhiteSpace(draft.TabId))
            .Where(static draft => !string.IsNullOrWhiteSpace(draft.SqlText))
            .Where(draft => draft.SavedAtUtc >= cutoff)
            .OrderBy(static draft => draft.TabOrder)
            .ThenBy(static draft => draft.SavedAtUtc)
            .ToList();

        if (valid.Count != persisted.Count)
        {
            if (valid.Count == 0)
                AppSettingsStore.ClearSqlEditorSessionDrafts();
            else
                AppSettingsStore.SaveSqlEditorSessionDrafts(valid);
        }

        return valid;
    }

    public void SaveDrafts(IReadOnlyList<SqlEditorSessionDraftEntry> drafts)
    {
        IReadOnlyList<SqlEditorSessionDraftEntry> normalized = (drafts ?? [])
            .Where(static draft => !string.IsNullOrWhiteSpace(draft.TabId))
            .Where(static draft => !string.IsNullOrWhiteSpace(draft.SqlText))
            .OrderBy(static draft => draft.TabOrder)
            .ThenBy(static draft => draft.SavedAtUtc)
            .ToList();

        if (normalized.Count == 0)
        {
            AppSettingsStore.ClearSqlEditorSessionDrafts();
            return;
        }

        AppSettingsStore.SaveSqlEditorSessionDrafts(normalized);
    }

    public void ClearDrafts()
    {
        AppSettingsStore.ClearSqlEditorSessionDrafts();
    }
}
