using DBWeaver.UI.Services.SqlEditor;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class CompletionUsageStatsTests
{
    [Fact]
    public void RecordAccepted_TracksRecentOccurrences()
    {
        var store = new InMemoryFrequencyStore();
        var sut = new CompletionUsageStats(store);

        sut.RecordAccepted("users", profileId: null);
        sut.RecordAccepted("users", profileId: null);

        Assert.Equal(2, sut.CountRecentOccurrences("users", window: 10));
    }

    [Fact]
    public void GetFrequency_LoadsPersistedProfileData()
    {
        var store = new InMemoryFrequencyStore();
        store.Data["profile-a"] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["users"] = 27,
        };
        var sut = new CompletionUsageStats(store);

        int frequency = sut.GetFrequency("users", "profile-a");

        Assert.Equal(27, frequency);
    }

    private sealed class InMemoryFrequencyStore : ISqlCompletionFrequencyStore
    {
        public Dictionary<string, Dictionary<string, int>> Data { get; } = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, int> Load(string profileId)
        {
            if (!Data.TryGetValue(profileId, out Dictionary<string, int>? value))
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            return new Dictionary<string, int>(value, StringComparer.OrdinalIgnoreCase);
        }

        public void Save(string profileId, IReadOnlyDictionary<string, int> frequencies)
        {
            Data[profileId] = frequencies.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }
    }
}
