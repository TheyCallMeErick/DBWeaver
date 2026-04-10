using Avalonia.Input;

namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Represents a keyboard gesture with display and normalized forms.
/// </summary>
public sealed record ShortcutGesture
{
    private const KeyModifiers SupportedModifiersMask =
        KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta;

    public Key Key { get; }
    public KeyModifiers Modifiers { get; }
    public string DisplayText { get; }
    public string NormalizedText { get; }

    public ShortcutGesture(
        Key key,
        KeyModifiers modifiers,
        string displayText,
        string normalizedText)
    {
        if (string.IsNullOrWhiteSpace(displayText))
            throw new ArgumentException("Display text cannot be null or whitespace.", nameof(displayText));

        if (string.IsNullOrWhiteSpace(normalizedText))
            throw new ArgumentException("Normalized text cannot be null or whitespace.", nameof(normalizedText));

        Key = key;
        Modifiers = NormalizeModifiers(modifiers);
        DisplayText = displayText.Trim();
        NormalizedText = normalizedText.Trim();
    }

    public static KeyModifiers NormalizeModifiers(KeyModifiers modifiers) =>
        modifiers & SupportedModifiersMask;
}
