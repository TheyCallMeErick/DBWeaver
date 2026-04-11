using DBWeaver.UI.Services.Settings;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class AppSettingsSqlCompletionFrequencyStore : ISqlCompletionFrequencyStore
{
    public IReadOnlyDictionary<string, int> Load(string profileId)
    {
        return AppSettingsStore.LoadSqlEditorCompletionFrequency(profileId);
    }

    public void Save(string profileId, IReadOnlyDictionary<string, int> frequencies)
    {
        AppSettingsStore.SaveSqlEditorCompletionFrequency(profileId, frequencies);
    }
}
