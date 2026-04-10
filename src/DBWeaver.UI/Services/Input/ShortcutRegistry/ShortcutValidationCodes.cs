namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Stable validation codes for shortcut parsing and registry operations.
/// </summary>
public static class ShortcutValidationCodes
{
    public const string InvalidFormat = "shortcut.invalid_format";
    public const string UnknownKey = "shortcut.unknown_key";
    public const string UnknownAction = "shortcut.unknown_action";
    public const string DuplicateGesture = "shortcut.duplicate_gesture";
    public const string DisallowedEmpty = "shortcut.disallowed_empty";
    public const string NotCustomizable = "shortcut.not_customizable";
    public const string PersistenceFailure = "shortcut.persistence_failure";
}
