using System.Globalization;
using DBWeaver.UI.ViewModels.Canvas;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DBWeaver.UI.Services.Explain;

public sealed partial class MySqlExplainPlanParser : IMySqlExplainPlanParser
{
    public MySqlParsedPlan ParseJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return new MySqlParsedPlan([], null, null);

        using JsonDocument doc = JsonDocument.Parse(rawJson);
        int nodeSequence = 0;
        var nodes = new List<ExplainNode>();
        Traverse(doc.RootElement, parentNodeId: null, indent: 0, nodes, ref nodeSequence);

        return new MySqlParsedPlan(nodes, null, null);
    }

    public MySqlParsedPlan ParseAnalyze(string rawAnalyzeText)
    {
        if (string.IsNullOrWhiteSpace(rawAnalyzeText))
            return new MySqlParsedPlan([], null, null);

        MatchCollection lines = AnalyzeStepRegex().Matches(rawAnalyzeText);
        var nodes = new List<ExplainNode>(lines.Count);
        double? executionTimeMs = null;
        int nodeSequence = 0;
        var nodeIdByIndent = new Dictionary<int, string>();

        foreach (Match lineMatch in lines)
        {
            if (!lineMatch.Success)
                continue;

            string line = lineMatch.Value.TrimEnd();
            int indent = Math.Max(0, lineMatch.Groups["indent"].Value.Length / 2);
            string text = lineMatch.Groups["text"].Value.Trim();

            double? cost = TryParseDouble(CostRegex().Match(line), "value");
            long? planRows = TryParseLong(RowsRegex().Match(line), "value");
            long? actualRows = TryParseLong(ActualRowsRegex().Match(line), "value");
            long? loops = TryParseLong(LoopsRegex().Match(line), "value");
            double? actualStart = TryParseDouble(ActualTimeStartRegex().Match(line), "start");
            double? actualTotal = TryParseDouble(ActualTimeRegex().Match(line), "end");
            if (actualTotal.HasValue)
                executionTimeMs = Math.Max(executionTimeMs ?? 0, actualTotal.Value);

            string alert = ResolveAlert(text);
            string nodeId = $"my-an-{++nodeSequence}";
            string? parentNodeId = null;
            if (indent > 0 && nodeIdByIndent.TryGetValue(indent - 1, out string? resolvedParent))
                parentNodeId = resolvedParent;

            nodes.Add(
                new ExplainNode
                {
                    NodeId = nodeId,
                    ParentNodeId = parentNodeId,
                    NodeType = ResolveNodeType(text),
                    Detail = text,
                    Predicate = ExtractPredicate(text),
                    EstimatedCost = cost,
                    EstimatedRows = planRows,
                    ActualStartupTimeMs = actualStart,
                    ActualTotalTimeMs = actualTotal,
                    ActualLoops = loops,
                    ActualRows = actualRows,
                    IndentLevel = indent,
                    IsExpensive = !string.IsNullOrEmpty(alert),
                    AlertLabel = alert,
                }
            );
            nodeIdByIndent[indent] = nodeId;

            foreach (int key in nodeIdByIndent.Keys.Where(k => k > indent).ToList())
                nodeIdByIndent.Remove(key);
        }

        return new MySqlParsedPlan(nodes, null, executionTimeMs);
    }

    private static void Traverse(
        JsonElement element,
        string? parentNodeId,
        int indent,
        List<ExplainNode> nodes,
        ref int nodeSequence)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                    Traverse(item, parentNodeId, indent, nodes, ref nodeSequence);
                return;

            case JsonValueKind.Object:
                if (TryBuildNode(element, parentNodeId, indent, ref nodeSequence, out ExplainNode node))
                {
                    nodes.Add(node);
                    parentNodeId = node.NodeId;
                }

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                        Traverse(property.Value, parentNodeId, indent + 1, nodes, ref nodeSequence);
                }
                return;
        }
    }

    private static bool TryBuildNode(
        JsonElement element,
        string? parentNodeId,
        int indent,
        ref int nodeSequence,
        out ExplainNode node)
    {
        node = new ExplainNode();
        string nodeId = $"my-json-{++nodeSequence}";

        if (element.TryGetProperty("table_name", out JsonElement tableNameElement))
        {
            string tableName = tableNameElement.GetString() ?? "<unknown>";
            string accessType = element.TryGetProperty("access_type", out JsonElement accessTypeElement)
                ? accessTypeElement.GetString() ?? "unknown"
                : "unknown";

            long? rows = TryGetLong(element, "rows_examined_per_scan")
                ?? TryGetLong(element, "rows_produced_per_join");
            double? cost = TryGetCost(element);
            string alert = accessType.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                ? "SEQ SCAN"
                : string.Empty;

            node = new ExplainNode
            {
                NodeId = nodeId,
                ParentNodeId = parentNodeId,
                NodeType = "Table Access",
                Detail = $"{tableName} ({accessType})",
                RelationName = tableName,
                EstimatedCost = cost,
                EstimatedRows = rows,
                IndentLevel = indent,
                IsExpensive = !string.IsNullOrEmpty(alert),
                AlertLabel = alert,
            };
            return true;
        }

        if (element.TryGetProperty("using_filesort", out JsonElement filesort)
            && filesort.ValueKind == JsonValueKind.True)
        {
            node = new ExplainNode
            {
                NodeId = nodeId,
                ParentNodeId = parentNodeId,
                NodeType = "Sort",
                Detail = "Using filesort",
                IndentLevel = indent,
                IsExpensive = true,
                AlertLabel = "SORT",
            };
            return true;
        }

        if (element.TryGetProperty("attached_condition", out JsonElement attachedCondition)
            && attachedCondition.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(attachedCondition.GetString()))
        {
            string predicate = attachedCondition.GetString()!;
            node = new ExplainNode
            {
                NodeId = nodeId,
                ParentNodeId = parentNodeId,
                NodeType = "Filter",
                Detail = predicate,
                Predicate = predicate,
                IndentLevel = indent,
            };
            return true;
        }

        if (element.TryGetProperty("nested_loop", out JsonElement nestedLoop)
            && nestedLoop.ValueKind == JsonValueKind.Array)
        {
            node = new ExplainNode
            {
                NodeId = nodeId,
                ParentNodeId = parentNodeId,
                NodeType = "Nested Loop",
                Detail = $"nested_loop[{nestedLoop.GetArrayLength()}]",
                IndentLevel = indent,
            };
            return true;
        }

        if (TryGetCost(element) is double queryCost && element.TryGetProperty("query_block", out _))
        {
            node = new ExplainNode
            {
                NodeId = nodeId,
                ParentNodeId = parentNodeId,
                NodeType = "Query Block",
                Detail = "query_block",
                EstimatedCost = queryCost,
                IndentLevel = indent,
            };
            return true;
        }

        return false;
    }

    private static double? TryGetCost(JsonElement element)
    {
        if (!element.TryGetProperty("cost_info", out JsonElement costInfo)
            || costInfo.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (costInfo.TryGetProperty("query_cost", out JsonElement queryCost))
            return TryParseDouble(queryCost);

        double? read = costInfo.TryGetProperty("read_cost", out JsonElement readCost)
            ? TryParseDouble(readCost)
            : null;
        double? eval = costInfo.TryGetProperty("eval_cost", out JsonElement evalCost)
            ? TryParseDouble(evalCost)
            : null;

        if (!read.HasValue && !eval.HasValue)
            return null;

        return (read ?? 0) + (eval ?? 0);
    }

    private static long? TryGetLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number))
            return number;

        if (value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }

        return null;
    }

    private static double? TryParseDouble(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number))
            return number;

        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }

        return null;
    }

    private static double? TryParseDouble(Match match, string groupName)
    {
        if (!match.Success)
            return null;

        if (!double.TryParse(match.Groups[groupName].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
            return null;

        return value;
    }

    private static long? TryParseLong(Match match, string groupName)
    {
        if (!match.Success)
            return null;

        if (!long.TryParse(match.Groups[groupName].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out long value))
            return null;

        return value;
    }

    private static string ResolveNodeType(string text)
    {
        int idx = text.IndexOf(':');
        if (idx <= 0)
            idx = text.IndexOf("  ", StringComparison.Ordinal);
        string baseText = idx > 0 ? text[..idx] : text;
        return baseText.Trim();
    }

    private static string ResolveAlert(string text)
    {
        if (text.Contains("table scan", StringComparison.OrdinalIgnoreCase))
            return "SEQ SCAN";
        if (text.Contains("filesort", StringComparison.OrdinalIgnoreCase))
            return "SORT";
        if (text.Contains("nested loop", StringComparison.OrdinalIgnoreCase))
            return "LOOP";
        return string.Empty;
    }

    private static string? ExtractPredicate(string text)
    {
        int idx = text.IndexOf("filter", StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? text[idx..].Trim() : null;
    }

    [GeneratedRegex(@"^(?<indent>\s*)->\s*(?<text>.+)$", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex AnalyzeStepRegex();

    [GeneratedRegex(@"\bcost=(?<value>[0-9]+(?:\.[0-9]+)?)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex CostRegex();

    [GeneratedRegex(@"\brows=(?<value>[0-9]+)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RowsRegex();

    [GeneratedRegex(@"\bactual\s+time=[0-9]+(?:\.[0-9]+)?\.\.(?<end>[0-9]+(?:\.[0-9]+)?)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ActualTimeRegex();

    [GeneratedRegex(@"\)\s+\(actual\s+time=[0-9]+(?:\.[0-9]+)?\.\.[0-9]+(?:\.[0-9]+)?\s+rows=(?<value>[0-9]+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ActualRowsRegex();

    [GeneratedRegex(@"\bloops=(?<value>[0-9]+)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LoopsRegex();

    [GeneratedRegex(@"\bactual\s+time=(?<start>[0-9]+(?:\.[0-9]+)?)\.\.[0-9]+(?:\.[0-9]+)?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ActualTimeStartRegex();
}



