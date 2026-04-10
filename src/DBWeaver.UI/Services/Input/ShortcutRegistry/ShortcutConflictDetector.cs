namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Default conflict detector considering normalized gesture and shortcut context.
/// </summary>
public sealed class ShortcutConflictDetector : IShortcutConflictDetector
{
    public IReadOnlyList<ShortcutValidationIssue> DetectConflicts(IReadOnlyList<ShortcutDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var issues = new List<ShortcutValidationIssue>();
        for (int left = 0; left < definitions.Count; left++)
        {
            ShortcutDefinition a = definitions[left];
            if (a.EffectiveGesture is null)
                continue;

            for (int right = left + 1; right < definitions.Count; right++)
            {
                ShortcutDefinition b = definitions[right];
                if (b.EffectiveGesture is null)
                    continue;

                bool sameGesture = string.Equals(
                    a.EffectiveGesture.NormalizedText,
                    b.EffectiveGesture.NormalizedText,
                    StringComparison.OrdinalIgnoreCase);
                if (!sameGesture)
                    continue;

                if (!ContextsConflict(a.Context, b.Context))
                    continue;

                issues.Add(new ShortcutValidationIssue(
                    ShortcutValidationCodes.DuplicateGesture,
                    $"Gesture '{a.EffectiveGesture.NormalizedText}' conflicts between '{a.ActionId.Value}' and '{b.ActionId.Value}'.",
                    ActionId: a.ActionId.Value,
                    Gesture: a.EffectiveGesture.NormalizedText));
            }
        }

        return issues;
    }

    private static bool ContextsConflict(ShortcutContext left, ShortcutContext right)
    {
        if (left == right)
            return true;

        return left == ShortcutContext.Global || right == ShortcutContext.Global;
    }
}
