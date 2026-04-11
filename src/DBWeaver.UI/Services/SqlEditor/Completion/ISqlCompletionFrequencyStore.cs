namespace DBWeaver.UI.Services.SqlEditor;

public interface ISqlCompletionFrequencyStore
{
    IReadOnlyDictionary<string, int> Load(string profileId);

    void Save(string profileId, IReadOnlyDictionary<string, int> frequencies);
}
