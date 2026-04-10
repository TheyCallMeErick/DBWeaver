namespace DBWeaver.CanvasKit;

public interface ICanvasTableNode
{
    bool IsTableSource { get; }
    bool IsHighlighted { get; set; }
    string? FullName { get; }
    string? Title { get; }
    string? Alias { get; }
}

public static class CanvasTableHighlightEngine
{
    public static void ApplyHighlight(IEnumerable<ICanvasTableNode> nodes, string? highlightedTableName)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        string? normalizedFull = NormalizeTableReference(highlightedTableName);
        string? normalizedShort = normalizedFull is null ? null : normalizedFull.Split('.').Last();

        foreach (ICanvasTableNode node in nodes)
        {
            if (!node.IsTableSource)
            {
                node.IsHighlighted = false;
                continue;
            }

            node.IsHighlighted = normalizedShort is not null
                && MatchesHighlightedTable(node, normalizedFull!, normalizedShort);
        }
    }

    public static bool MatchesHighlightedTable(
        ICanvasTableNode node,
        string normalizedFull,
        string normalizedShort
    )
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedFull);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedShort);

        string? nodeFull = NormalizeTableReference(node.FullName);
        if (nodeFull is not null && nodeFull.Equals(normalizedFull, StringComparison.OrdinalIgnoreCase))
            return true;

        string? nodeTitle = NormalizeTableReference(node.Title);
        if (nodeTitle is not null
            && nodeTitle.Split('.').Last().Equals(normalizedShort, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string? nodeAlias = NormalizeTableReference(node.Alias);
        if (nodeAlias is not null
            && nodeAlias.Split('.').Last().Equals(normalizedShort, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public static string? NormalizeTableReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string raw = value.Trim();
        if (raw.Length == 0)
            return null;

        string firstToken = raw.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        string[] parts = firstToken.Split(
            '.',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return null;

        for (int i = 0; i < parts.Length; i++)
            parts[i] = parts[i].Trim('"').Trim('`').Trim('[', ']');

        return string.Join('.', parts);
    }
}
