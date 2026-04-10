using DBWeaver.Nodes;
using DBWeaver.Nodes.Pins;
using DBWeaver.UI.ViewModels.Validation.Conventions;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services.Validation;

// ── Validator ─────────────────────────────────────────────────────────────────

public static class GraphValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(
        CanvasViewModel vm,
        NamingConventionPolicy? namingPolicy = null,
        IAliasConventionRegistry? aliasConventionRegistry = null)
    {
        var issues = new List<ValidationIssue>();
        namingPolicy ??= NamingConventionPolicy.Default;
        aliasConventionRegistry ??= AliasConventionRegistry.CreateDefault();

        // Build a fast lookup: which input pins have at least one wire coming in
        var connectedInputs = new HashSet<PinViewModel>(
            vm.Connections.Where(c => c.ToPin is not null).Select(c => c.ToPin!)
        );

        // Build a set of node IDs that have at least one wire (part of the flow)
        var nodesInFlow = new HashSet<string>(
            vm.Connections.SelectMany(c =>
            {
                var ids = new List<string> { c.FromPin.Owner.Id };
                if (c.ToPin is not null)
                    ids.Add(c.ToPin.Owner.Id);
                return ids;
            })
        );

        // Rule: no table source on the canvas (global warning, not per-node)
        if (
            vm.Nodes.Any()
            && !vm.Nodes.Any(n =>
                n.Type is NodeType.TableSource or NodeType.CteSource or NodeType.Subquery or NodeType.SubqueryReference
            )
        )
            issues.Add(
                new ValidationIssue(
                    IssueSeverity.Warning,
                    "",
                    "NO_TABLE",
                    "No data source on canvas",
                    "Add a Table Source, CTE Source, or Subquery node from the search menu (Shift+A)"
                )
            );

        // Collect alias names for duplicate detection
        var aliasGroups = vm
            .Nodes.Where(n => !string.IsNullOrWhiteSpace(n.Alias))
            .GroupBy(n => n.Alias!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Select(n => n.Id))
            .ToHashSet();

        // Rule: ResultOutput node must have at least one column connected
        foreach (NodeViewModel? n in vm.Nodes.Where(n => n.Type is NodeType.ResultOutput or NodeType.SelectOutput))
        {
            if (n.OutputColumnOrder.Count == 0)
                issues.Add(
                    new ValidationIssue(
                        IssueSeverity.Warning,
                        n.Id,
                        "EMPTY_RESULT_OUTPUT",
                        "Result Output has no columns connected",
                        "Connect column or expression output pins to this node's input"
                    )
                );
        }

        // Rule: reject structural mismatches on wires (e.g. RowSet -> ColumnRef)
        foreach (ConnectionViewModel conn in vm.Connections.Where(c => c.ToPin is not null))
        {
            PinViewModel fromPin = conn.FromPin;
            PinViewModel toPin = conn.ToPin!;
            var decision = toPin.EvaluateConnection(fromPin);

            if (decision.IsAllowed)
            {
                // Rule: detect broad Expression flow where a typed ColumnRef was expected.
                if (
                    fromPin.EffectiveDataType == PinDataType.Expression
                    && toPin.EffectiveDataType == PinDataType.ColumnRef
                    && fromPin.Owner.Type is not (NodeType.ColumnRefCast or NodeType.ScalarFromColumn)
                )
                {
                    issues.Add(
                        new ValidationIssue(
                            IssueSeverity.Warning,
                            toPin.Owner.Id,
                            "UNJUSTIFIED_EXPRESSION_PIN",
                            $"Expression output feeding ColumnRef input: {fromPin.Owner.Title}.{fromPin.Name} -> {toPin.Owner.Title}.{toPin.Name}",
                            "Prefer typed ColumnRef nodes; when coercion is intentional, use ColumnRefCast or ScalarFromColumn"
                        )
                    );
                }

                continue;
            }

            bool isStructural = decision.ReasonCode == PinConnectionReasonCode.StructuralTypeMismatch;
            string sourceLabel = $"{fromPin.Owner.Title}.{fromPin.Name} ({fromPin.EffectiveDataType})";
            string destinationLabel = $"{toPin.Owner.Title}.{toPin.Name} ({toPin.EffectiveDataType})";
            string reasonCode = PinConnectionReasonCatalog.ToIssueCode(decision.ReasonCode);
            string message = isStructural
                ? $"Invalid structural connection: '{fromPin.EffectiveDataType}' → '{toPin.EffectiveDataType}'"
                : PinConnectionReasonCatalog.ToDiagnosticMessage(decision.ReasonCode, sourceLabel, destinationLabel);
            string suggestion = isStructural
                ? "Connect row-set pins only to row-set inputs, and column pins to column/expression inputs"
                : "Connect pins with compatible semantic types (e.g. ColumnRef->ColumnRef, Boolean->Boolean, RowSet->RowSet)";

            issues.Add(new ValidationIssue(
                IssueSeverity.Error,
                toPin.Owner.Id,
                reasonCode,
                message,
                suggestion));

            if (!isStructural && !string.Equals(reasonCode, "PIN_TYPE_MISMATCH", StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue(
                    IssueSeverity.Error,
                    toPin.Owner.Id,
                    "PIN_TYPE_MISMATCH",
                    message,
                    suggestion));
            }
        }

        // Detect orphan nodes and emit a warning per orphan
        IReadOnlySet<string> orphanIds = OrphanNodeDetector.DetectOrphanIds(vm);
        foreach (string orphanId in orphanIds)
        {
            issues.Add(
                new ValidationIssue(
                    IssueSeverity.Warning,
                    orphanId,
                    "ORPHAN_NODE",
                    "This node does not contribute to the query output",
                    "Connect it to the flow or delete it to keep the canvas clean"
                )
            );
        }

        foreach (NodeViewModel node in vm.Nodes)
        {
            bool partOfFlow = nodesInFlow.Contains(node.Id);

            // ── Rule: required input pins must be connected (only for nodes in the flow)
            if (partOfFlow)
            {
                foreach (PinViewModel? pin in node.InputPins.Where(p => p.IsRequired))
                {
                    if (!connectedInputs.Contains(pin))
                        issues.Add(
                            new ValidationIssue(
                                IssueSeverity.Error,
                                node.Id,
                                "UNCONNECTED_PIN",
                                $"Required input '{pin.Name}' is not connected",
                                "Connect a wire from an upstream node to this pin"
                            )
                        );
                }
            }

            // ── Rule: Alias node must have an alias name
            if (node.Type == NodeType.Alias)
            {
                bool hasAlias =
                    node.Parameters.TryGetValue("alias", out string? a)
                    && !string.IsNullOrWhiteSpace(a);
                if (!hasAlias)
                    issues.Add(
                        new ValidationIssue(
                            IssueSeverity.Warning,
                            node.Id,
                            "EMPTY_ALIAS",
                            "Alias name is empty",
                            "Enter an alias name in the property panel"
                        )
                    );
            }

            // ── Rule: duplicate alias across nodes
            if (aliasGroups.Contains(node.Id))
                issues.Add(
                    new ValidationIssue(
                        IssueSeverity.Warning,
                        node.Id,
                        "DUPLICATE_ALIAS",
                        $"Alias '{node.Alias}' is used by multiple nodes",
                        "Use a unique alias for each node"
                    )
                );

            // ── Rule: pattern-based nodes need a pattern parameter
            IEnumerable<string> requiredParams = RequiredParamsFor(node.Type);
            foreach (string param in requiredParams)
            {
                bool filled =
                    node.Parameters.TryGetValue(param, out string? v)
                    && !string.IsNullOrWhiteSpace(v);
                if (!filled)
                    issues.Add(
                        new ValidationIssue(
                            IssueSeverity.Warning,
                            node.Id,
                            "MISSING_PARAM",
                            $"Parameter '{param}' is required for this node",
                            "Set the value in the property panel"
                        )
                    );
            }
        }

        // ── Rule: naming convention violations on aliases ─────────────────────
        foreach (NodeViewModel? node in vm.Nodes.Where(n => !string.IsNullOrWhiteSpace(n.Alias)))
        {
            IReadOnlyList<AliasViolation> namingViolations =
                NamingConventionValidator.CheckAlias(node.Alias!, namingPolicy, aliasConventionRegistry);
            foreach (AliasViolation violation in namingViolations)
                issues.Add(
                    new ValidationIssue(
                        IssueSeverity.Warning,
                        node.Id,
                        violation.Code,
                        violation.Message,
                        violation.Suggestion)
                );
        }

        return issues;
    }

    private static IEnumerable<string> RequiredParamsFor(NodeType type) =>
        type switch
        {
            NodeType.RegexMatch => ["pattern"],
            NodeType.RegexReplace => ["pattern", "replacement"],
            NodeType.RegexExtract => ["pattern"],
            NodeType.Replace => ["search"],
            NodeType.ValueMap => ["src", "dst"],
            NodeType.JsonExtract => ["path"],
            NodeType.Substring => ["start"],
            _ => [],
        };
}
