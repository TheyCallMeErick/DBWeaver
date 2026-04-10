namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Resolves user-facing messages for stable shortcut validation codes.
/// </summary>
public static class ShortcutValidationMessageResolver
{
    public static string Resolve(ShortcutValidationIssue? issue)
    {
        if (issue is null)
            return "Unable to update shortcut.";

        return Resolve(issue.Code, issue.Message);
    }

    public static string Resolve(string? code, string? fallbackMessage)
    {
        string normalizedCode = code?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedCode))
            return string.IsNullOrWhiteSpace(fallbackMessage) ? "Unable to update shortcut." : fallbackMessage!;

        return normalizedCode switch
        {
            ShortcutValidationCodes.InvalidFormat => "Shortcut format is invalid. Use values like Ctrl+Shift+P or F5.",
            ShortcutValidationCodes.UnknownKey => "Shortcut key is unknown or not supported in this layout.",
            ShortcutValidationCodes.UnknownAction => "Shortcut action is unknown.",
            ShortcutValidationCodes.DuplicateGesture => "Shortcut is already in use by another action.",
            ShortcutValidationCodes.DisallowedEmpty => "Empty shortcut is not allowed for this action.",
            ShortcutValidationCodes.NotCustomizable => "This shortcut cannot be customized.",
            ShortcutValidationCodes.PersistenceFailure => "Could not persist shortcut changes. Your previous shortcuts are still active.",
            _ => string.IsNullOrWhiteSpace(fallbackMessage) ? "Unable to update shortcut." : fallbackMessage!,
        };
    }
}

