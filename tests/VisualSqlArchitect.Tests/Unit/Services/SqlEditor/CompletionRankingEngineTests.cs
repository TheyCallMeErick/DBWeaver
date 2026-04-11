using DBWeaver.UI.Services.SqlEditor;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class CompletionRankingEngineTests
{
    [Fact]
    public void Rank_EmptyPrefix_SortsByKindThenAlphabetical()
    {
        var sut = new CompletionRankingEngine();
        var usage = new CompletionUsageStats(new InMemoryFrequencyStore());
        var symbolTable = new SqlSymbolTable([], new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        IReadOnlyList<SqlCompletionSuggestion> raw =
        [
            new("zeta", "zeta", null, SqlCompletionKind.Column),
            new("alpha", "alpha", null, SqlCompletionKind.Keyword),
            new("beta", "beta", null, SqlCompletionKind.Column),
        ];

        IReadOnlyList<SqlCompletionSuggestion> ranked = sut.Rank(raw, string.Empty, symbolTable, usage, profileId: null);

        Assert.Equal("alpha", ranked[0].Label);
        Assert.Equal("beta", ranked[1].Label);
        Assert.Equal("zeta", ranked[2].Label);
    }

    [Fact]
    public void Rank_ExactPrefix_BeatsFuzzyMatches()
    {
        var sut = new CompletionRankingEngine();
        var usage = new CompletionUsageStats(new InMemoryFrequencyStore());
        var symbolTable = new SqlSymbolTable([], new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        IReadOnlyList<SqlCompletionSuggestion> raw =
        [
            new("order_id", "order_id", null, SqlCompletionKind.Column),
            new("user_id", "user_id", null, SqlCompletionKind.Column),
            new("id", "id", null, SqlCompletionKind.Column),
        ];

        IReadOnlyList<SqlCompletionSuggestion> ranked = sut.Rank(raw, "id", symbolTable, usage, profileId: null);

        Assert.Equal("id", ranked[0].Label);
    }

    [Fact]
    public void Rank_RecentUsage_BoostsCandidateWithinSamePrefixBucket()
    {
        var sut = new CompletionRankingEngine();
        var usage = new CompletionUsageStats(new InMemoryFrequencyStore());
        var symbolTable = new SqlSymbolTable([], new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        usage.RecordAccepted("status", profileId: null);

        IReadOnlyList<SqlCompletionSuggestion> raw =
        [
            new("state", "state", null, SqlCompletionKind.Column),
            new("status", "status", null, SqlCompletionKind.Column),
        ];

        IReadOnlyList<SqlCompletionSuggestion> ranked = sut.Rank(raw, "st", symbolTable, usage, profileId: null);

        Assert.Equal("status", ranked[0].Label);
    }

    [Fact]
    public void Rank_FiltersOutZeroScoreCandidates()
    {
        var sut = new CompletionRankingEngine();
        var usage = new CompletionUsageStats(new InMemoryFrequencyStore());
        var symbolTable = new SqlSymbolTable([], new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        IReadOnlyList<SqlCompletionSuggestion> raw =
        [
            new("users", "users", null, SqlCompletionKind.Table),
            new("orders", "orders", null, SqlCompletionKind.Table),
        ];

        IReadOnlyList<SqlCompletionSuggestion> ranked = sut.Rank(raw, "usr", symbolTable, usage, profileId: null);

        Assert.Single(ranked);
        Assert.Equal("users", ranked[0].Label);
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
