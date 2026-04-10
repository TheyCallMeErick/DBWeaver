using Avalonia.Input;

namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Initial shortcut registry implementation with in-memory override and reset support.
/// </summary>
public sealed class ShortcutRegistry : IShortcutRegistry
{
    private readonly ShortcutCustomizationLoader _customizationLoader;
    private readonly IShortcutCustomizationStore _customizationStore;
    private readonly IReadOnlyList<ShortcutDefinition> _defaultDefinitions;
    private IReadOnlyList<ShortcutDefinition> _effectiveDefinitions;
    private IReadOnlyList<ShortcutValidationIssue> _issues;
    private IReadOnlyList<ShortcutCustomizationEntry> _overrides;

    public ShortcutRegistry(
        DefaultShortcutCatalog? defaultCatalog = null,
        IShortcutGestureParser? gestureParser = null,
        IShortcutConflictDetector? conflictDetector = null,
        IShortcutCustomizationStore? customizationStore = null,
        ShortcutCustomizationLoader? customizationLoader = null)
    {
        IShortcutGestureParser effectiveParser = gestureParser ?? new ShortcutGestureParser();
        IShortcutConflictDetector effectiveConflictDetector = conflictDetector ?? new ShortcutConflictDetector();
        _customizationStore = customizationStore ?? new AppSettingsShortcutCustomizationStore();
        _customizationLoader = customizationLoader ?? new ShortcutCustomizationLoader(effectiveParser, effectiveConflictDetector);
        _defaultDefinitions = (defaultCatalog ?? new DefaultShortcutCatalog(effectiveParser)).Build();
        _effectiveDefinitions = _defaultDefinitions.Select(static item => item with { EffectiveGesture = item.DefaultGesture }).ToList();
        _issues = [];
        _overrides = [];
        _ = Reload();
    }

    public ShortcutRegistrySnapshot GetSnapshot() =>
        new(_effectiveDefinitions, _issues);

    public IReadOnlyList<ShortcutDefinition> GetAll() => _effectiveDefinitions;

    public ShortcutDefinition? FindByActionId(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId))
            return null;

        return _effectiveDefinitions.FirstOrDefault(d =>
            string.Equals(d.ActionId.Value, actionId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public ShortcutDefinition? FindByGesture(Key key, KeyModifiers modifiers, ShortcutContext context)
    {
        KeyModifiers normalizedModifiers = ShortcutGesture.NormalizeModifiers(modifiers);
        ShortcutDefinition? exactContext = _effectiveDefinitions.FirstOrDefault(d =>
            d.EffectiveGesture is not null
            && d.EffectiveGesture.Key == key
            && d.EffectiveGesture.Modifiers == normalizedModifiers
            && d.Context == context);

        if (exactContext is not null)
            return exactContext;

        if (context == ShortcutContext.Global)
            return null;

        return _effectiveDefinitions.FirstOrDefault(d =>
            d.EffectiveGesture is not null
            && d.EffectiveGesture.Key == key
            && d.EffectiveGesture.Modifiers == normalizedModifiers
            && d.Context == ShortcutContext.Global);
    }

    public ShortcutReloadResult Reload()
    {
        IReadOnlyList<ShortcutCustomizationEntry> loadedOverrides = _customizationStore.LoadOverrides();
        ShortcutCustomizationLoadResult loadResult = _customizationLoader.ApplyOverrides(_defaultDefinitions, loadedOverrides);

        _overrides = loadResult.AppliedOverrides;
        _effectiveDefinitions = loadResult.Snapshot.Definitions;
        _issues = loadResult.Snapshot.Issues;
        return new ShortcutReloadResult(Success: true, loadResult.Snapshot);
    }

    public ShortcutUpdateResult TryOverride(string actionId, string? gestureText)
    {
        ShortcutDefinition? target = FindByActionId(actionId);
        if (target is null)
            return Failed(
                ShortcutValidationCodes.UnknownAction,
                $"Shortcut action '{actionId}' is not known.",
                actionId,
                gestureText);

        if (!target.AllowCustomization)
        {
            return Failed(
                ShortcutValidationCodes.NotCustomizable,
                $"Shortcut action '{actionId}' is not customizable.",
                actionId,
                gestureText);
        }

        if (string.IsNullOrWhiteSpace(gestureText))
            return ResetToDefault(actionId);

        string normalizedActionId = target.ActionId.Value;
        List<ShortcutCustomizationEntry> proposedOverrides = BuildProposedOverrides(normalizedActionId, gestureText);
        ShortcutCustomizationLoadResult loadResult = _customizationLoader.ApplyOverrides(_defaultDefinitions, proposedOverrides);
        if (HasNewIssues(_issues, loadResult.Snapshot.Issues))
            return new ShortcutUpdateResult(false, GetSnapshot(), loadResult.Snapshot.Issues);

        if (!_customizationStore.TrySaveOverrides(loadResult.AppliedOverrides))
        {
            return Failed(
                ShortcutValidationCodes.PersistenceFailure,
                "Shortcut customization could not be persisted.",
                normalizedActionId,
                gestureText);
        }

        _overrides = loadResult.AppliedOverrides;
        _effectiveDefinitions = loadResult.Snapshot.Definitions;
        _issues = loadResult.Snapshot.Issues;
        return new ShortcutUpdateResult(true, GetSnapshot(), []);
    }

    public ShortcutUpdateResult ResetToDefault(string actionId)
    {
        ShortcutDefinition? target = FindByActionId(actionId);
        if (target is null)
        {
            return Failed(
                ShortcutValidationCodes.UnknownAction,
                $"Shortcut action '{actionId}' is not known.",
                actionId,
                gesture: null);
        }

        ShortcutDefinition? defaultDefinition = _defaultDefinitions.FirstOrDefault(d =>
            string.Equals(d.ActionId.Value, target.ActionId.Value, StringComparison.OrdinalIgnoreCase));
        if (defaultDefinition is null)
        {
            return Failed(
                ShortcutValidationCodes.UnknownAction,
                $"Default shortcut action '{actionId}' is not known.",
                actionId,
                gesture: null);
        }

        List<ShortcutCustomizationEntry> proposedOverrides = _overrides
            .Where(overrideEntry => !string.Equals(overrideEntry.ActionId, actionId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        ShortcutCustomizationLoadResult loadResult = _customizationLoader.ApplyOverrides(_defaultDefinitions, proposedOverrides);
        if (HasNewIssues(_issues, loadResult.Snapshot.Issues))
            return new ShortcutUpdateResult(false, GetSnapshot(), loadResult.Snapshot.Issues);

        if (!_customizationStore.TrySaveOverrides(loadResult.AppliedOverrides))
        {
            return Failed(
                ShortcutValidationCodes.PersistenceFailure,
                "Shortcut customization could not be persisted.",
                actionId,
                gesture: null);
        }

        _overrides = loadResult.AppliedOverrides;
        _effectiveDefinitions = loadResult.Snapshot.Definitions;
        _issues = loadResult.Snapshot.Issues;
        return new ShortcutUpdateResult(true, GetSnapshot(), []);
    }

    public ShortcutUpdateResult ResetAllToDefault()
    {
        ShortcutCustomizationLoadResult loadResult = _customizationLoader.ApplyOverrides(_defaultDefinitions, []);
        if (!_customizationStore.TrySaveOverrides([]))
        {
            return Failed(
                ShortcutValidationCodes.PersistenceFailure,
                "Shortcut customization could not be persisted.",
                actionId: null,
                gesture: null);
        }

        _overrides = loadResult.AppliedOverrides;
        _effectiveDefinitions = loadResult.Snapshot.Definitions;
        _issues = loadResult.Snapshot.Issues;
        return new ShortcutUpdateResult(true, GetSnapshot(), []);
    }

    private ShortcutUpdateResult Failed(string code, string message, string? actionId, string? gesture)
    {
        var issues = new List<ShortcutValidationIssue>
        {
            new(code, message, actionId, gesture),
        };
        return new ShortcutUpdateResult(false, GetSnapshot(), issues);
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

    private List<ShortcutCustomizationEntry> BuildProposedOverrides(string actionId, string? gestureText)
    {
        var overrides = _overrides
            .Where(overrideEntry => !string.Equals(overrideEntry.ActionId, actionId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!string.IsNullOrWhiteSpace(gestureText))
            overrides.Add(new ShortcutCustomizationEntry(actionId, gestureText.Trim()));

        return overrides;
    }
}
