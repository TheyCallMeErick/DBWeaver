namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Applies persisted shortcut overrides with per-item fallback and validation issues.
/// </summary>
public sealed class ShortcutCustomizationLoader
{
    private readonly IShortcutGestureParser _gestureParser;
    private readonly IShortcutConflictDetector _conflictDetector;

    public ShortcutCustomizationLoader(
        IShortcutGestureParser gestureParser,
        IShortcutConflictDetector conflictDetector)
    {
        _gestureParser = gestureParser ?? throw new ArgumentNullException(nameof(gestureParser));
        _conflictDetector = conflictDetector ?? throw new ArgumentNullException(nameof(conflictDetector));
    }

    public ShortcutCustomizationLoadResult ApplyOverrides(
        IReadOnlyList<ShortcutDefinition> defaultDefinitions,
        IReadOnlyList<ShortcutCustomizationEntry> overrides)
    {
        ArgumentNullException.ThrowIfNull(defaultDefinitions);
        ArgumentNullException.ThrowIfNull(overrides);

        var acceptedOverrides = new List<ShortcutCustomizationEntry>();
        var issues = new List<ShortcutValidationIssue>();
        var definitions = defaultDefinitions.Select(static item => item with { EffectiveGesture = item.DefaultGesture }).ToList();

        IReadOnlyList<ShortcutValidationIssue> currentConflicts = _conflictDetector.DetectConflicts(definitions);
        if (currentConflicts.Count > 0)
            issues.AddRange(currentConflicts);

        foreach (ShortcutCustomizationEntry entry in overrides)
        {
            if (string.IsNullOrWhiteSpace(entry.ActionId))
            {
                issues.Add(new ShortcutValidationIssue(
                    ShortcutValidationCodes.UnknownAction,
                    "Shortcut action id is required.",
                    ActionId: null,
                    Gesture: entry.Gesture));
                continue;
            }

            string actionId = entry.ActionId.Trim();
            ShortcutDefinition? target = definitions.FirstOrDefault(definition =>
                string.Equals(definition.ActionId.Value, actionId, StringComparison.OrdinalIgnoreCase));
            if (target is null)
            {
                issues.Add(new ShortcutValidationIssue(
                    ShortcutValidationCodes.UnknownAction,
                    $"Shortcut action '{actionId}' is not known.",
                    ActionId: actionId,
                    Gesture: entry.Gesture));
                continue;
            }

            if (!target.AllowCustomization)
            {
                issues.Add(new ShortcutValidationIssue(
                    ShortcutValidationCodes.NotCustomizable,
                    $"Shortcut action '{actionId}' is not customizable.",
                    ActionId: actionId,
                    Gesture: entry.Gesture));
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Gesture))
            {
                acceptedOverrides.Add(new ShortcutCustomizationEntry(actionId, null));
                definitions = definitions
                    .Select(definition =>
                        string.Equals(definition.ActionId.Value, actionId, StringComparison.OrdinalIgnoreCase)
                            ? definition with { EffectiveGesture = definition.DefaultGesture }
                            : definition)
                    .ToList();
                continue;
            }

            ShortcutGestureParseResult parseResult = _gestureParser.Parse(entry.Gesture);
            if (parseResult.Gesture is null)
            {
                issues.Add(new ShortcutValidationIssue(
                    parseResult.Issue?.Code ?? ShortcutValidationCodes.InvalidFormat,
                    parseResult.Issue?.Message ?? "Shortcut gesture is invalid.",
                    ActionId: actionId,
                    Gesture: entry.Gesture));
                continue;
            }

            List<ShortcutDefinition> candidate = definitions
                .Select(definition =>
                    string.Equals(definition.ActionId.Value, actionId, StringComparison.OrdinalIgnoreCase)
                        ? definition with { EffectiveGesture = parseResult.Gesture }
                        : definition)
                .ToList();

            IReadOnlyList<ShortcutValidationIssue> candidateIssues = _conflictDetector.DetectConflicts(candidate);
            if (HasNewIssues(currentConflicts, candidateIssues))
            {
                issues.Add(new ShortcutValidationIssue(
                    ShortcutValidationCodes.DuplicateGesture,
                    $"Gesture '{parseResult.Gesture.NormalizedText}' conflicts with another active shortcut.",
                    ActionId: actionId,
                    Gesture: parseResult.Gesture.NormalizedText));
                continue;
            }

            definitions = candidate;
            currentConflicts = candidateIssues;
            acceptedOverrides.Add(new ShortcutCustomizationEntry(actionId, entry.Gesture.Trim()));
        }

        return new ShortcutCustomizationLoadResult(
            new ShortcutRegistrySnapshot(definitions, issues),
            acceptedOverrides);
    }

    private static bool HasNewIssues(
        IReadOnlyList<ShortcutValidationIssue> baseline,
        IReadOnlyList<ShortcutValidationIssue> candidate)
    {
        if (candidate.Count == 0)
            return false;

        if (baseline.Count == 0)
            return true;

        var baselineKeys = baseline
            .Select(ToIssueKey)
            .ToHashSet(StringComparer.Ordinal);

        foreach (ShortcutValidationIssue issue in candidate)
        {
            if (!baselineKeys.Contains(ToIssueKey(issue)))
                return true;
        }

        return false;
    }

    private static string ToIssueKey(ShortcutValidationIssue issue) =>
        $"{issue.Code}|{issue.ActionId}|{issue.Gesture}|{issue.Message}";
}
