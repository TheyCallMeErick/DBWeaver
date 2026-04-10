namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Parses and normalizes textual keyboard gestures.
/// </summary>
public interface IShortcutGestureParser
{
    ShortcutGestureParseResult Parse(string? gestureText);
}
