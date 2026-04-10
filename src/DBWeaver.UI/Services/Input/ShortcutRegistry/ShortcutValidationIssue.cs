namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Structured validation issue for parsing, conflicts or invalid override operations.
/// </summary>
public sealed record ShortcutValidationIssue(
    string Code,
    string Message,
    string? ActionId,
    string? Gesture);
