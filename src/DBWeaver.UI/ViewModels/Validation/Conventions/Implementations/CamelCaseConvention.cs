using System.Text.RegularExpressions;
using DBWeaver.UI.ViewModels.Validation.Conventions.Rules;

namespace DBWeaver.UI.ViewModels.Validation.Conventions.Implementations;

public sealed partial class CamelCaseConvention : IAutoFixableConvention
{
    public string ConventionName => "camelCase";

    [GeneratedRegex(@"^[a-z][a-zA-Z0-9]*$", RegexOptions.CultureInvariant)]
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
                    "NAMING_CAMEL_CASE",
                    $"Alias '{alias}' does not follow camelCase convention",
                    $"Rename to '{Normalize(alias)}'."));

        return violations;
    }

    public string Normalize(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return alias;

        IReadOnlyList<string> words = AliasWordTokenizer.SplitWords(alias);
        if (words.Count == 0)
            return "alias";

        string head = words[0];
        string tail = string.Concat(words.Skip(1).Select(Capitalize));
        string normalized = head + tail;
        if (normalized.Length > 0 && char.IsDigit(normalized[0]))
            normalized = "a" + normalized;

        return normalized;
    }

    private static string Capitalize(string input) =>
        string.IsNullOrEmpty(input)
            ? input
            : char.ToUpperInvariant(input[0]) + input[1..].ToLowerInvariant();
}

