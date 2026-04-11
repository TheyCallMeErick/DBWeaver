namespace DBWeaver.UI.Services.SqlEditor;

public sealed class CompletionRankingEngine
{
    private const int PrefixExactBonus = 1000;
    private const int ActiveTableBonus = 50;
    private const int RecentPerOccurrenceBonus = 30;
    private const int RecentBonusCap = 90;
    private const int FrequencyStep = 10;
    private const int FrequencyBonusPerStep = 10;
    private const int FrequencyBonusCap = 50;

    private readonly FuzzyScorer _fuzzyScorer;

    public CompletionRankingEngine(FuzzyScorer? fuzzyScorer = null)
    {
        _fuzzyScorer = fuzzyScorer ?? new FuzzyScorer();
    }

    public IReadOnlyList<SqlCompletionSuggestion> Rank(
        IReadOnlyList<SqlCompletionSuggestion> rawSuggestions,
        string prefix,
        SqlSymbolTable symbolTable,
        CompletionUsageStats usageStats,
        string? profileId)
    {
        string normalizedPrefix = prefix?.Trim() ?? string.Empty;
        if (rawSuggestions.Count == 0)
            return [];

        if (string.IsNullOrWhiteSpace(normalizedPrefix))
        {
            return rawSuggestions
                .OrderBy(static s => s.Kind)
                .ThenBy(static s => s.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        SqlTableBindingSymbol? activeTable = symbolTable.BindingsInOrder.LastOrDefault(static b => !b.IsSubquery);

        var ranked = rawSuggestions
            .Select(s => BuildRanked(s, normalizedPrefix, activeTable, usageStats, profileId))
            .Where(static item => item.FuzzyScore > 0 || item.IsPrefixExact)
            .OrderByDescending(static item => item.IsPrefixExact)
            .ThenByDescending(static item => item.IsExcellentFuzzy)
            .ThenByDescending(static item => item.ActiveTableBonus)
            .ThenByDescending(static item => item.RecentBonus)
            .ThenByDescending(static item => item.FuzzyScore)
            .ThenByDescending(static item => item.FrequencyBonus)
            .ThenBy(static item => item.Suggestion.Kind)
            .ThenBy(static item => item.Suggestion.Label, StringComparer.OrdinalIgnoreCase)
            .Select(static item => item.Suggestion)
            .ToList();

        return ranked;
    }

    private Ranked BuildRanked(
        SqlCompletionSuggestion suggestion,
        string prefix,
        SqlTableBindingSymbol? activeTable,
        CompletionUsageStats usageStats,
        string? profileId)
    {
        string identity = ExtractIdentity(suggestion.Label);
        string fuzzyCandidate = GetFuzzyCandidate(suggestion, identity);
        bool isPrefixExact =
            identity.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || fuzzyCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        int fuzzyScore = Math.Max(
            _fuzzyScorer.Score(prefix, identity),
            _fuzzyScorer.Score(prefix, fuzzyCandidate));
        int activeTableBonus = ComputeActiveTableBonus(suggestion, activeTable);

        int recentOccurrences = usageStats.CountRecentOccurrences(identity, window: 10);
        int recentBonus = Math.Min(recentOccurrences * RecentPerOccurrenceBonus, RecentBonusCap);

        int frequency = usageStats.GetFrequency(identity, profileId);
        int frequencyBonus = Math.Min((frequency / FrequencyStep) * FrequencyBonusPerStep, FrequencyBonusCap);

        return new Ranked(
            suggestion,
            IsPrefixExact: isPrefixExact,
            IsExcellentFuzzy: fuzzyScore >= 80,
            FuzzyScore: isPrefixExact ? Math.Max(fuzzyScore, PrefixExactBonus) : fuzzyScore,
            ActiveTableBonus: activeTableBonus,
            RecentBonus: recentBonus,
            FrequencyBonus: frequencyBonus);
    }

    private static int ComputeActiveTableBonus(SqlCompletionSuggestion suggestion, SqlTableBindingSymbol? activeTable)
    {
        if (activeTable is null)
            return 0;

        string label = suggestion.Label;
        string activeAlias = activeTable.Alias;
        string activeShort = activeTable.TableRef.Split('.').Last();

        if (suggestion.Kind == SqlCompletionKind.Column)
        {
            if (label.StartsWith($"{activeAlias}.", StringComparison.OrdinalIgnoreCase))
                return ActiveTableBonus;

            if (!string.IsNullOrWhiteSpace(suggestion.Detail)
                && suggestion.Detail.Contains($"{activeTable.TableRef}.", StringComparison.OrdinalIgnoreCase))
            {
                return ActiveTableBonus;
            }
        }

        if (suggestion.Kind == SqlCompletionKind.Table)
        {
            if (string.Equals(label, activeTable.TableRef, StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, activeShort, StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, activeAlias, StringComparison.OrdinalIgnoreCase))
            {
                return ActiveTableBonus;
            }
        }

        return 0;
    }

    private static string ExtractIdentity(string label)
    {
        string value = label?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        int asIndex = value.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase);
        if (asIndex > 0)
            value = value[..asIndex];

        if (value.Contains('.'))
            value = value.Split('.').Last();

        return value.Trim('"', '\'', '`', '[', ']');
    }

    private static string GetFuzzyCandidate(SqlCompletionSuggestion suggestion, string identity)
    {
        if (suggestion.Kind == SqlCompletionKind.Table || suggestion.Kind == SqlCompletionKind.Join)
            return suggestion.Label;

        return identity;
    }

    private sealed record Ranked(
        SqlCompletionSuggestion Suggestion,
        bool IsPrefixExact,
        bool IsExcellentFuzzy,
        int FuzzyScore,
        int ActiveTableBonus,
        int RecentBonus,
        int FrequencyBonus);
}
