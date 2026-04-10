using System.Text.RegularExpressions;
using DBWeaver.UI.ViewModels.Validation.Conventions.Rules;

namespace DBWeaver.UI.ViewModels.Validation.Conventions.Implementations;

public sealed partial class ScreamingSnakeCaseConvention : IAutoFixableConvention
{
    public string ConventionName => "SCREAMING_SNAKE_CASE";

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public IReadOnlyList<AliasViolation> Check(string alias)
    {
        var violations = new List<AliasViolation>(4);

        if (CommonAliasRules.CheckSpaces(alias) is { } spaces)
            violations.Add(spaces);
        if (CommonAliasRules.CheckLeadingDigit(alias) is { } digit)
            violations.Add(digit);
        if (CommonAliasRules.CheckMaxLength(alias, 64) is { } length)
            violations.Add(length);

        if (!Pattern().IsMatch(alias) && violations.Count == 0)
            violations.Add(
                new AliasViolation(
                    "NAMING_SCREAMING_SNAKE_CASE",
                    $"Alias '{alias}' does not follow SCREAMING_SNAKE_CASE convention",
                    $"Rename to '{Normalize(alias)}'."));

        return violations;
    }

    public string Normalize(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return alias;

        IReadOnlyList<string> words = AliasWordTokenizer.SplitWords(alias);
        if (words.Count == 0)
            return "ALIAS";

        var normalizedWords = words.ToList();
        if (normalizedWords.Count >= 2
            && normalizedWords[0].All(char.IsDigit)
            && normalizedWords[1].All(char.IsLetter))
        {
            normalizedWords[0] = normalizedWords[0] + normalizedWords[1];
            normalizedWords.RemoveAt(1);
        }

        string normalized = string.Join("_", normalizedWords).ToUpperInvariant();
        if (normalized.Length > 0 && char.IsDigit(normalized[0]))
            normalized = "_" + normalized;

        return normalized;
    }
}
