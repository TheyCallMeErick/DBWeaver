namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Stable semantic identifier for a shortcut action.
/// </summary>
public sealed record ShortcutActionId
{
    public string Value { get; }

    public ShortcutActionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Shortcut action id cannot be null or whitespace.", nameof(value));

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
