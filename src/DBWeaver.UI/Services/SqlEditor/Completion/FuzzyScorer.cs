namespace DBWeaver.UI.Services.SqlEditor;

public sealed class FuzzyScorer
{
    public int Score(string pattern, string candidate)
    {
        string needle = pattern?.Trim() ?? string.Empty;
        string haystack = candidate?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(needle) || string.IsNullOrWhiteSpace(haystack))
            return 0;

        int searchIndex = 0;
        int previousMatchIndex = -2;
        int rawScore = 0;

        for (int i = 0; i < needle.Length; i++)
        {
            char target = char.ToLowerInvariant(needle[i]);
            int found = IndexOfIgnoreCase(haystack, target, searchIndex);
            if (found < 0)
                return 0;

            rawScore += 10;
            if (found == previousMatchIndex + 1)
                rawScore += 15;

            if (IsSegmentStart(haystack, found))
                rawScore += 20;

            previousMatchIndex = found;
            searchIndex = found + 1;
        }

        int normalizationBase = Math.Max(needle.Length * 45, 1);
        int normalized = (int)Math.Round((rawScore * 100d) / normalizationBase);
        return Math.Clamp(normalized, 1, 100);
    }

    private static bool IsSegmentStart(string value, int index)
    {
        if (index <= 0)
            return true;

        char previous = value[index - 1];
        return previous is '_' or '.' or '-' or ' ';
    }

    private static int IndexOfIgnoreCase(string value, char target, int startIndex)
    {
        for (int i = Math.Max(0, startIndex); i < value.Length; i++)
        {
            if (char.ToLowerInvariant(value[i]) == target)
                return i;
        }

        return -1;
    }
}
