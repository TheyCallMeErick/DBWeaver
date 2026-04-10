namespace DBWeaver.Nodes;

/// <summary>
/// Resolves colored node tags for catalog discoverability and search.
/// </summary>
public static class NodeTagCatalog
{
    private static readonly IReadOnlyDictionary<string, string> Palette = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["source"] = NodeVisualColorConstants.Source,
        ["transform"] = NodeVisualColorConstants.Transform,
        ["comparison"] = NodeVisualColorConstants.Comparison,
        ["logic"] = NodeVisualColorConstants.Logic,
        ["json"] = NodeVisualColorConstants.Json,
        ["aggregate"] = NodeVisualColorConstants.Aggregate,
        ["output"] = NodeVisualColorConstants.Output,
        ["literal"] = NodeVisualColorConstants.Literal,
        ["ddl"] = NodeVisualColorConstants.Ddl,
        ["join"] = NodeVisualColorConstants.Join,
        ["subquery"] = NodeVisualColorConstants.Subquery,
        ["cte"] = NodeVisualColorConstants.Cte,
        ["where"] = NodeVisualColorConstants.Where,
        ["group"] = NodeVisualColorConstants.Group,
        ["order"] = NodeVisualColorConstants.Order,
        ["top"] = NodeVisualColorConstants.Top,
        ["export"] = NodeVisualColorConstants.Export,
        ["string"] = NodeVisualColorConstants.String,
        ["math"] = NodeVisualColorConstants.Math,
        ["date"] = NodeVisualColorConstants.Date,
        ["cast"] = NodeVisualColorConstants.Cast,
        ["report"] = NodeVisualColorConstants.Report,
        ["sql"] = NodeVisualColorConstants.Sql,
    };

    private static readonly IReadOnlyDictionary<NodeCategory, string> CategoryTagByCategory = new Dictionary<NodeCategory, string>
    {
        [NodeCategory.DataSource] = "source",
        [NodeCategory.StringTransform] = "string",
        [NodeCategory.MathTransform] = "math",
        [NodeCategory.TypeCast] = "cast",
        [NodeCategory.Comparison] = "comparison",
        [NodeCategory.LogicGate] = "logic",
        [NodeCategory.Json] = "json",
        [NodeCategory.Aggregate] = "aggregate",
        [NodeCategory.Conditional] = "transform",
        [NodeCategory.ResultModifier] = "transform",
        [NodeCategory.Output] = "output",
        [NodeCategory.Literal] = "literal",
        [NodeCategory.Ddl] = "ddl",
    };

    public static IReadOnlyList<NodeTag> Resolve(NodeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var map = new Dictionary<string, NodeTag>(StringComparer.OrdinalIgnoreCase);

        if (CategoryTagByCategory.TryGetValue(definition.Category, out string? categoryTag))
            AddTag(map, categoryTag);

        foreach (NodeTag explicitTag in definition.Tags ?? [])
            AddTag(map, explicitTag.Name, explicitTag.ColorHex);

        AddHeuristicTags(definition, map);
        return map.Values.ToList();
    }

    public static bool MatchesSearch(NodeDefinition definition, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        string normalized = query.Trim();
        if (definition.DisplayName.Contains(normalized, StringComparison.OrdinalIgnoreCase)
            || definition.Description.Contains(normalized, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Resolve(definition).Any(tag =>
            tag.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddHeuristicTags(NodeDefinition definition, IDictionary<string, NodeTag> map)
    {
        string haystack = $"{definition.DisplayName} {definition.Description} {definition.Type}"
            .ToLowerInvariant();

        if (haystack.Contains("join", StringComparison.Ordinal))
            AddTag(map, "join");
        if (haystack.Contains("subquery", StringComparison.Ordinal))
            AddTag(map, "subquery");
        if (haystack.Contains("cte", StringComparison.Ordinal))
            AddTag(map, "cte");
        if (haystack.Contains("where", StringComparison.Ordinal))
            AddTag(map, "where");
        if (haystack.Contains("group", StringComparison.Ordinal))
            AddTag(map, "group");
        if (haystack.Contains("order", StringComparison.Ordinal))
            AddTag(map, "order");
        if (haystack.Contains("top", StringComparison.Ordinal) || haystack.Contains("limit", StringComparison.Ordinal))
            AddTag(map, "top");
        if (haystack.Contains("export", StringComparison.Ordinal) || haystack.Contains("csv", StringComparison.Ordinal)
            || haystack.Contains("json", StringComparison.Ordinal) || haystack.Contains("excel", StringComparison.Ordinal))
            AddTag(map, "export");
        if (haystack.Contains("date", StringComparison.Ordinal) || haystack.Contains("time", StringComparison.Ordinal))
            AddTag(map, "date");
        if (haystack.Contains("sql", StringComparison.Ordinal))
            AddTag(map, "sql");
        if (haystack.Contains("report", StringComparison.Ordinal))
            AddTag(map, "report");

        bool hasReportPin = definition.Pins.Any(p => p.DataType == PinDataType.ReportQuery);
        if (hasReportPin)
        {
            AddTag(map, "report");
            AddTag(map, "sql");
        }
    }

    private static void AddTag(IDictionary<string, NodeTag> map, string tagName, string? colorHex = null)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            return;

        string normalized = tagName.Trim().ToLowerInvariant();
        if (map.ContainsKey(normalized))
            return;

        string resolvedColor = !string.IsNullOrWhiteSpace(colorHex)
            ? colorHex
            : Palette.TryGetValue(normalized, out string? mappedColor)
                ? mappedColor
                : NodeVisualColorConstants.Fallback;

        map[normalized] = new NodeTag(normalized, resolvedColor);
    }
}
