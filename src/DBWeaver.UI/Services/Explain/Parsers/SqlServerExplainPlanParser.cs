using System.Globalization;
using DBWeaver.UI.ViewModels.Canvas;
using System.Xml.Linq;

namespace DBWeaver.UI.Services.Explain;

public sealed class SqlServerExplainPlanParser : ISqlServerExplainPlanParser
{
    public SqlServerParsedPlan Parse(string rawXml)
    {
        if (string.IsNullOrWhiteSpace(rawXml))
            return new SqlServerParsedPlan([], null, null);

        XDocument doc = XDocument.Parse(rawXml);
        XNamespace ns = doc.Root?.Name.NamespaceName
            ?? "http://schemas.microsoft.com/sqlserver/2004/07/showplan";

        var relOps = doc.Descendants(ns + "RelOp").ToList();
        var nodeIdsByElement = new Dictionary<XElement, string>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < relOps.Count; i++)
            nodeIdsByElement[relOps[i]] = $"ss-{i + 1}";

        var nodes = new List<ExplainNode>();
        foreach (XElement relOp in relOps)
            nodes.Add(MapRelOp(relOp, ns, nodeIdsByElement));

        return new SqlServerParsedPlan(nodes, null, null);
    }

    private static ExplainNode MapRelOp(
        XElement relOp,
        XNamespace ns,
        IReadOnlyDictionary<XElement, string> nodeIdsByElement)
    {
        string physicalOp = relOp.Attribute("PhysicalOp")?.Value ?? "RelOp";
        string nodeId = nodeIdsByElement.TryGetValue(relOp, out string? id) ? id : Guid.NewGuid().ToString("N");
        XElement? parent = relOp.Parent?.AncestorsAndSelf(ns + "RelOp").FirstOrDefault();
        string? parentId = parent is not null && nodeIdsByElement.TryGetValue(parent, out string? pid) ? pid : null;

        double? estimatedCost = TryGetDouble(relOp.Attribute("EstimatedTotalSubtreeCost")?.Value);
        long? estimatedRows = TryGetLong(relOp.Attribute("EstimateRows")?.Value);
        long? actualRows = TryGetLong(relOp.Attribute("ActualRows")?.Value);
        long? actualExecutions = TryGetLong(relOp.Attribute("ActualExecutions")?.Value);
        int indent = relOp.Ancestors(ns + "RelOp").Count();
        string detail = BuildDetail(relOp, ns, physicalOp, out string? relation, out string? indexName, out string? predicate);
        string alert = ResolveAlert(physicalOp);
        double? actualElapsed = TryGetDouble(
            relOp.Descendants(ns + "RunTimeCountersPerThread")
                .Select(e => e.Attribute("ActualElapsedms")?.Value)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)));

        return new ExplainNode
        {
            NodeId = nodeId,
            ParentNodeId = parentId,
            NodeType = physicalOp,
            Detail = detail,
            RelationName = relation,
            IndexName = indexName,
            Predicate = predicate,
            EstimatedCost = estimatedCost,
            EstimatedRows = estimatedRows,
            ActualRows = actualRows,
            ActualLoops = actualExecutions,
            ActualTotalTimeMs = actualElapsed,
            IndentLevel = indent,
            IsExpensive = !string.IsNullOrEmpty(alert),
            AlertLabel = alert,
        };
    }

    private static string BuildDetail(
        XElement relOp,
        XNamespace ns,
        string fallback,
        out string? relation,
        out string? indexName,
        out string? predicate)
    {
        relation = null;
        indexName = null;
        predicate = null;

        XElement? obj = relOp.Descendants(ns + "Object").FirstOrDefault();
        relation = obj?.Attribute("Table")?.Value;
        indexName = obj?.Attribute("Index")?.Value;
        predicate = relOp.Descendants(ns + "Predicate")
            .Attributes("ScalarString")
            .Select(a => a.Value)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        var parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(relation))
            parts.Add(relation);
        if (!string.IsNullOrWhiteSpace(indexName))
            parts.Add($"index={indexName}");
        if (!string.IsNullOrWhiteSpace(predicate))
            parts.Add($"predicate={predicate}");

        return parts.Count == 0 ? fallback : string.Join(" | ", parts);
    }

    private static string ResolveAlert(string physicalOp)
    {
        if (physicalOp.Contains("Table Scan", StringComparison.OrdinalIgnoreCase))
            return "SEQ SCAN";
        if (physicalOp.Contains("Sort", StringComparison.OrdinalIgnoreCase))
            return "SORT";
        return string.Empty;
    }

    private static double? TryGetDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
            return null;

        return parsed;
    }

    private static long? TryGetLong(string? value)
    {
        double? parsed = TryGetDouble(value);
        if (!parsed.HasValue)
            return null;

        return Convert.ToInt64(Math.Round(parsed.Value));
    }
}



