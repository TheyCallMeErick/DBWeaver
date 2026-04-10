
namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryPreviewDiagnosticContextEnricher
{
    public void Enrich(CanvasViewModel canvas, List<PreviewDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0 || canvas.Nodes.Count == 0)
            return;

        for (int i = 0; i < diagnostics.Count; i++)
        {
            PreviewDiagnostic diagnostic = diagnostics[i];
            if (!string.IsNullOrWhiteSpace(diagnostic.NodeId))
                continue;

            if (!TryResolveDiagnosticTarget(canvas, diagnostic.Message, out string? nodeId, out string? pinName))
                continue;

            diagnostics[i] = new PreviewDiagnostic(
                diagnostic.Severity,
                diagnostic.Category,
                diagnostic.Code,
                diagnostic.Message,
                nodeId,
                pinName
            );
        }
    }

    private static bool TryResolveDiagnosticTarget(CanvasViewModel canvas, string message, out string? nodeId, out string? pinName)
    {
        nodeId = null;
        pinName = null;

        if (string.IsNullOrWhiteSpace(message))
            return false;

        Match aliasAmbiguityMatch = Regex.Match(
            message,
            @"nodes\s+'([^']+)'\s+and\s+'([^']+)'",
            RegexOptions.IgnoreCase
        );
        if (aliasAmbiguityMatch.Success)
        {
            string firstNodeTitle = aliasAmbiguityMatch.Groups[1].Value.Trim();
            NodeViewModel? byTitle = canvas.Nodes.FirstOrDefault(n =>
                n.Title.Equals(firstNodeTitle, StringComparison.OrdinalIgnoreCase)
            );
            if (byTitle is not null)
            {
                nodeId = byTitle.Id;
                return true;
            }
        }

        List<string> quotedTerms = Regex
            .Matches(message, "'([^']+)'")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        NodeViewModel? matchedNode = null;

        foreach (string term in quotedTerms)
        {
            List<NodeViewModel> nodesByIdentity = canvas
                .Nodes.Where(n =>
                    n.Title.Equals(term, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(n.Alias, term, StringComparison.OrdinalIgnoreCase)
                    || TryGetParameter(n, "table").Equals(term, StringComparison.OrdinalIgnoreCase)
                    || TryGetParameter(n, "cte_name").Equals(term, StringComparison.OrdinalIgnoreCase)
                    || TryGetParameter(n, "name").Equals(term, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();

            if (nodesByIdentity.Count == 1)
            {
                matchedNode = nodesByIdentity[0];
                break;
            }

            if (
                nodesByIdentity.Count > 1
                && message.Contains("alias ambiguity", StringComparison.OrdinalIgnoreCase)
            )
            {
                matchedNode = nodesByIdentity.OrderBy(n => n.Id).First();
                break;
            }
        }

        foreach (string term in quotedTerms)
        {
            if (matchedNode is not null)
            {
                PinViewModel? pinOnMatchedNode = matchedNode
                    .AllPins.FirstOrDefault(p => p.Name.Equals(term, StringComparison.OrdinalIgnoreCase));
                if (pinOnMatchedNode is not null)
                {
                    pinName = pinOnMatchedNode.Name;
                    break;
                }
            }

            List<(NodeViewModel Node, PinViewModel Pin)> nodesByPin = canvas
                .Nodes.SelectMany(n => n.AllPins.Select(p => (Node: n, Pin: p)))
                .Where(x => x.Pin.Name.Equals(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (nodesByPin.Count == 1)
            {
                matchedNode = nodesByPin[0].Node;
                pinName = nodesByPin[0].Pin.Name;
                break;
            }
        }

        if (matchedNode is null)
            return false;

        nodeId = matchedNode.Id;
        return true;
    }

    private static string TryGetParameter(NodeViewModel node, string key) =>
        node.Parameters.TryGetValue(key, out string? value) ? value ?? string.Empty : string.Empty;
}



