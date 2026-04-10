namespace DBWeaver.UI.Services.CommandPalette;

internal static class FuzzyScorer
{
    public static int Score(PaletteCommandItem item, string query)
    {
        if (string.IsNullOrEmpty(query))
            return 1;

        string q = query.ToLowerInvariant();
        string[] tokens = q.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length > 1)
        {
            int total = 0;
            foreach (string token in tokens)
            {
                int tokenScore = ScoreSingle(item, token);
                if (tokenScore <= 0)
                    return 0;

                total += tokenScore;
            }

            return total + 25;
        }

        return ScoreSingle(item, q);
    }

    private static int ScoreSingle(PaletteCommandItem item, string q)
    {
        string name = item.Name.ToLowerInvariant();
        string desc = item.Description.ToLowerInvariant();
        string tags = item.Tags.ToLowerInvariant();
        string shortcut = item.Shortcut.ToLowerInvariant();

        if (name == q)
            return 400;

        if (name.StartsWith(q))
            return 300 + (100 - Math.Min(name.Length, 100));

        if (shortcut == q)
            return 280;

        if (shortcut.StartsWith(q))
            return 220;

        int span = SubsequenceSpan(name, q);
        if (span >= 0)
        {
            int bonus = Math.Max(0, 99 - (span - q.Length));
            return 200 + bonus;
        }

        if (desc.Contains(q) || tags.Contains(q) || shortcut.Contains(q))
            return 100;

        if (SubsequenceSpan(desc, q) >= 0
            || SubsequenceSpan(tags, q) >= 0
            || SubsequenceSpan(shortcut, q) >= 0)
            return 50;

        return 0;
    }

    private static int SubsequenceSpan(string text, string pattern)
    {
        int pi = 0;
        int start = -1;

        for (int ti = 0; ti < text.Length; ti++)
        {
            if (text[ti] != pattern[pi])
                continue;

            if (pi == 0)
                start = ti;
            pi++;

            if (pi == pattern.Length)
                return ti - start + 1;
        }

        return -1;
    }
}

