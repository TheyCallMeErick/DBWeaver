namespace DBWeaver.CanvasKit;

public static class CanvasAutoJoinSemantics
{
    public static bool TrySplitJoinClauseOnEquality(
        string? clause,
        out string left,
        out string right)
    {
        left = string.Empty;
        right = string.Empty;

        if (string.IsNullOrWhiteSpace(clause))
            return false;

        string text = clause.Trim();
        int eq = text.IndexOf('=');
        if (eq <= 0 || eq >= text.Length - 1)
            return false;

        // Accept only a single plain equality operator (no >=, <=, !=, ==, chained expressions).
        if (text.Contains(">=", StringComparison.Ordinal)
            || text.Contains("<=", StringComparison.Ordinal)
            || text.Contains("!=", StringComparison.Ordinal)
            || text.Contains("==", StringComparison.Ordinal))
        {
            return false;
        }

        if (text.IndexOf('=', eq + 1) >= 0)
            return false;

        left = text[..eq].Trim();
        right = text[(eq + 1)..].Trim();

        return left.Length > 0 && right.Length > 0;
    }

    public static string BuildSuggestionPairKey(
        string existingTable,
        string newTable,
        string leftColumn,
        string rightColumn)
    {
        string[] pair = [existingTable ?? string.Empty, newTable ?? string.Empty];
        Array.Sort(pair, StringComparer.OrdinalIgnoreCase);
        return $"{pair[0]}|{pair[1]}|{leftColumn}|{rightColumn}";
    }

    public static bool TryParseQualifiedColumn(
        string expression,
        out string? source,
        out string column)
    {
        source = null;
        column = string.Empty;

        if (string.IsNullOrWhiteSpace(expression))
            return false;

        string normalized = expression.Trim();
        int lastDot = normalized.LastIndexOf('.');
        if (lastDot <= 0 || lastDot >= normalized.Length - 1)
            return false;

        source = normalized[..lastDot].Trim();
        column = normalized[(lastDot + 1)..].Trim();

        if (column.Length == 0)
            return false;

        source = source.Trim('"').Trim('`').Trim('[', ']');
        column = column.Trim('"').Trim('`').Trim('[', ']');

        return source.Length > 0 && column.Length > 0;
    }

    public static bool MatchesSource(
        string fullName,
        string shortName,
        string? alias,
        string sourceRef)
    {
        if (string.IsNullOrWhiteSpace(sourceRef))
            return false;

        if (fullName.Equals(sourceRef, StringComparison.OrdinalIgnoreCase))
            return true;

        if (shortName.Equals(sourceRef, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(alias)
            && alias.Equals(sourceRef, StringComparison.OrdinalIgnoreCase))
            return true;

        string fullShort = fullName.Split('.').Last();
        return fullShort.Equals(sourceRef, StringComparison.OrdinalIgnoreCase);
    }
}
