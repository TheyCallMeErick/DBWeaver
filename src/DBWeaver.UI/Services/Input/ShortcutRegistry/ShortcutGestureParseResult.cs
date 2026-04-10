namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Parse result for a gesture text input.
/// </summary>
public sealed record ShortcutGestureParseResult(
    ShortcutGesture? Gesture,
    ShortcutValidationIssue? Issue)
{
    public bool IsSuccess => Gesture is not null && Issue is null;
}
