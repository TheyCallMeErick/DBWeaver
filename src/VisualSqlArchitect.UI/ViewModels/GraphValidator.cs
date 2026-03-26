using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.UI.ViewModels;

// ── Diagnostic model ─────────────────────────────────────────────────────────

public enum IssueSeverity { Error, Warning }

public sealed record ValidationIssue(
    IssueSeverity Severity,
    string        NodeId,
    string        Code,
    string        Message,
    string?       Suggestion = null
);

// ── Validator ─────────────────────────────────────────────────────────────────

public static class GraphValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(CanvasViewModel vm)
    {
        var issues = new List<ValidationIssue>();

        // Build a fast lookup: which input pins have at least one wire coming in
        var connectedInputs = new HashSet<PinViewModel>(
            vm.Connections
              .Where(c => c.ToPin is not null)
              .Select(c => c.ToPin!));

        // Build a set of node IDs that have at least one wire (part of the flow)
        var nodesInFlow = new HashSet<string>(
            vm.Connections.SelectMany(c =>
            {
                var ids = new List<string> { c.FromPin.Owner.Id };
                if (c.ToPin is not null) ids.Add(c.ToPin.Owner.Id);
                return ids;
            }));

        // Rule: no table source on the canvas (global warning, not per-node)
        if (vm.Nodes.Any() && !vm.Nodes.Any(n => n.Type == NodeType.TableSource))
            issues.Add(new ValidationIssue(IssueSeverity.Warning, "",
                "NO_TABLE", "No table source on canvas",
                "Add a table node from the search menu (Shift+A)"));

        // Collect alias names for duplicate detection
        var aliasGroups = vm.Nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.Alias))
            .GroupBy(n => n.Alias!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Select(n => n.Id))
            .ToHashSet();

        // Rule: ResultOutput node must have at least one column connected
        foreach (var n in vm.Nodes.Where(n => n.Type == NodeType.ResultOutput))
        {
            if (n.OutputColumnOrder.Count == 0)
                issues.Add(new ValidationIssue(IssueSeverity.Warning, n.Id,
                    "EMPTY_RESULT_OUTPUT",
                    "Result Output has no columns connected",
                    "Connect column or expression output pins to this node's input"));
        }

        foreach (var node in vm.Nodes)
        {
            bool partOfFlow = nodesInFlow.Contains(node.Id);

            // ── Rule: required input pins must be connected (only for nodes in the flow)
            if (partOfFlow)
            {
                foreach (var pin in node.InputPins.Where(p => p.IsRequired))
                {
                    if (!connectedInputs.Contains(pin))
                        issues.Add(new ValidationIssue(IssueSeverity.Error, node.Id,
                            "UNCONNECTED_PIN",
                            $"Required input '{pin.Name}' is not connected",
                            "Connect a wire from an upstream node to this pin"));
                }
            }

            // ── Rule: Alias node must have an alias name
            if (node.Type == NodeType.Alias)
            {
                var hasAlias = node.Parameters.TryGetValue("alias", out var a)
                               && !string.IsNullOrWhiteSpace(a);
                if (!hasAlias)
                    issues.Add(new ValidationIssue(IssueSeverity.Warning, node.Id,
                        "EMPTY_ALIAS",
                        "Alias name is empty",
                        "Enter an alias name in the property panel"));
            }

            // ── Rule: duplicate alias across nodes
            if (aliasGroups.Contains(node.Id))
                issues.Add(new ValidationIssue(IssueSeverity.Warning, node.Id,
                    "DUPLICATE_ALIAS",
                    $"Alias '{node.Alias}' is used by multiple nodes",
                    "Use a unique alias for each node"));

            // ── Rule: pattern-based nodes need a pattern parameter
            var requiredParams = RequiredParamsFor(node.Type);
            foreach (var param in requiredParams)
            {
                var filled = node.Parameters.TryGetValue(param, out var v)
                             && !string.IsNullOrWhiteSpace(v);
                if (!filled)
                    issues.Add(new ValidationIssue(IssueSeverity.Warning, node.Id,
                        "MISSING_PARAM",
                        $"Parameter '{param}' is required for this node",
                        "Set the value in the property panel"));
            }
        }

        return issues;
    }

    private static IEnumerable<string> RequiredParamsFor(NodeType type) => type switch
    {
        NodeType.RegexMatch   => ["pattern"],
        NodeType.RegexReplace => ["pattern", "replacement"],
        NodeType.RegexExtract => ["pattern"],
        NodeType.Replace      => ["search"],
        NodeType.ValueMap     => ["src", "dst"],
        NodeType.JsonExtract  => ["path"],
        NodeType.Substring    => ["start"],
        _                     => []
    };
}
