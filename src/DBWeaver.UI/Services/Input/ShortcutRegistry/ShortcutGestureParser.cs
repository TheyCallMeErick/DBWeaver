using Avalonia.Input;

namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Default parser for textual keyboard gestures.
/// </summary>
public sealed class ShortcutGestureParser : IShortcutGestureParser
{
    public ShortcutGestureParseResult Parse(string? gestureText)
    {
        if (string.IsNullOrWhiteSpace(gestureText))
        {
            return new ShortcutGestureParseResult(
                null,
                new ShortcutValidationIssue(
                    ShortcutValidationCodes.DisallowedEmpty,
                    "Shortcut gesture is empty.",
                    ActionId: null,
                    Gesture: gestureText));
        }

        string normalizedTextInput = PreNormalizeInput(gestureText.Trim());
        string[] tokens = normalizedTextInput
            .Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 0)
            return InvalidFormat(gestureText);

        KeyModifiers modifiers = KeyModifiers.None;
        for (int index = 0; index < tokens.Length - 1; index++)
        {
            string token = tokens[index];
            if (!ShortcutKeyAliasMap.TryResolveModifier(token, out KeyModifiers modifier))
                return InvalidFormat(gestureText);

            modifiers |= modifier;
        }

        string keyToken = tokens[^1];
        if (ShortcutKeyAliasMap.TryResolveModifier(keyToken, out _))
            return InvalidFormat(gestureText);

        if (!ShortcutKeyAliasMap.TryResolveKey(keyToken, out Key key) || key == Key.None)
        {
            return new ShortcutGestureParseResult(
                null,
                new ShortcutValidationIssue(
                    ShortcutValidationCodes.UnknownKey,
                    $"Unknown shortcut key token '{keyToken}'.",
                    ActionId: null,
                    Gesture: gestureText));
        }

        KeyModifiers normalizedModifiers = ShortcutGesture.NormalizeModifiers(modifiers);
        string normalizedGesture = BuildGestureText(key, normalizedModifiers, display: false);
        string displayGesture = BuildGestureText(key, normalizedModifiers, display: true);

        return new ShortcutGestureParseResult(
            new ShortcutGesture(key, normalizedModifiers, displayGesture, normalizedGesture),
            null);
    }

    private static ShortcutGestureParseResult InvalidFormat(string? input)
        => new(
            null,
            new ShortcutValidationIssue(
                ShortcutValidationCodes.InvalidFormat,
                "Shortcut gesture format is invalid.",
                ActionId: null,
                Gesture: input));

    private static string PreNormalizeInput(string input)
    {
        if (input.Contains("++", StringComparison.Ordinal))
            return input.Replace("++", "+Plus", StringComparison.Ordinal);

        return input;
    }

    private static string BuildGestureText(Key key, KeyModifiers modifiers, bool display)
    {
        var parts = new List<string>();

        if (modifiers.HasFlag(KeyModifiers.Control))
            parts.Add("Ctrl");
        if (modifiers.HasFlag(KeyModifiers.Alt))
            parts.Add("Alt");
        if (modifiers.HasFlag(KeyModifiers.Shift))
            parts.Add("Shift");
        if (modifiers.HasFlag(KeyModifiers.Meta))
            parts.Add("Meta");

        parts.Add(display
            ? ShortcutKeyAliasMap.ToDisplayKey(key)
            : ShortcutKeyAliasMap.ToNormalizedKey(key));

        return string.Join("+", parts);
    }
}
