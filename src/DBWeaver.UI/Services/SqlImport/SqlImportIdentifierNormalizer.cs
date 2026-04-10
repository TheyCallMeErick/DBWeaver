using System.Text.RegularExpressions;

namespace DBWeaver.UI.Services.SqlImport;

internal static class SqlImportIdentifierNormalizer
{
    public const string IdentifierPattern = "(?:\"(?:[^\"]|\"\")*\"|\\[[^\\]]+\\]|`[^`]+`|[A-Za-z_][A-Za-z0-9_]*)";
    public const string QualifiedIdentifierPattern = IdentifierPattern + "(?:\\s*\\.\\s*" + IdentifierPattern + ")*";

    public static string NormalizeQualifiedIdentifier(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        MatchCollection matches = Regex.Matches(raw, IdentifierPattern, RegexOptions.CultureInvariant);
        if (matches.Count == 0)
            return raw.Trim();

        return string.Join('.', matches
            .Select(match => NormalizeIdentifierToken(match.Value))
            .Where(token => !string.IsNullOrWhiteSpace(token)));
    }

    public static string NormalizeIdentifierToken(string token)
    {
        string trimmed = token.Trim();
        if (trimmed.Length >= 2)
        {
            if ((trimmed.StartsWith('"') && trimmed.EndsWith('"'))
                || (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                || (trimmed.StartsWith('`') && trimmed.EndsWith('`')))
            {
                trimmed = trimmed[1..^1];
            }
        }

        return trimmed.Replace("\"\"", "\"", StringComparison.Ordinal);
    }
}
