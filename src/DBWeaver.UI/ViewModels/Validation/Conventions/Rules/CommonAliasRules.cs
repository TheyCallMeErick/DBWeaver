namespace DBWeaver.UI.ViewModels.Validation.Conventions.Rules;

internal static class CommonAliasRules
{
    internal static AliasViolation? CheckSpaces(string alias) =>
        alias.Contains(' ', StringComparison.Ordinal)
            ? new AliasViolation(
                "NAMING_SPACES",
                $"Alias '{alias}' contains spaces",
                "Remove spaces or use separators compatible with the selected convention.")
            : null;

    internal static AliasViolation? CheckLeadingDigit(string alias) =>
        alias.Length > 0 && char.IsDigit(alias[0])
            ? new AliasViolation(
                "NAMING_LEADING_DIGIT",
                $"Alias '{alias}' starts with a digit",
                "SQL identifiers should start with a letter or underscore.")
            : null;

    internal static AliasViolation? CheckMaxLength(string alias, int maxLength) =>
        maxLength > 0 && alias.Length > maxLength
            ? new AliasViolation(
                "NAMING_TOO_LONG",
                $"Alias '{alias}' exceeds maximum length of {maxLength} characters",
                $"Shorten to {maxLength} characters or fewer.")
            : null;
}

