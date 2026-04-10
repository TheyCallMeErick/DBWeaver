using System.Text.RegularExpressions;

namespace DBWeaver.UI.ViewModels.Validation.Conventions.Rules;

internal static partial class AliasWordTokenizer
{
    [GeneratedRegex(@"[A-Z]+(?![a-z])|[A-Z]?[a-z]+|\d+", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();

    /// <summary>
    /// Splits an alias into normalized words preserving acronym boundaries.
    /// Example: HTTPSStatus_v2 => [https, status, v, 2].
    /// </summary>
    internal static IReadOnlyList<string> SplitWords(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return [];

        string normalized = Regex.Replace(alias.Trim(), @"[^A-Za-z0-9]+", " ");
        MatchCollection matches = WordRegex().Matches(normalized);
        if (matches.Count == 0)
            return [];

        return matches
            .Select(m => m.Value.ToLowerInvariant())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();
    }
}

