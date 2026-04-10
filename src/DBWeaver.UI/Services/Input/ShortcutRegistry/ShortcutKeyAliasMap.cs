using Avalonia.Input;

namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Alias map for shortcut parser key and modifier tokens.
/// </summary>
public static class ShortcutKeyAliasMap
{
    private static readonly IReadOnlyDictionary<string, Key> KeyAliases =
        new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase)
        {
            ["0"] = Key.D0,
            ["1"] = Key.D1,
            ["2"] = Key.D2,
            ["3"] = Key.D3,
            ["4"] = Key.D4,
            ["5"] = Key.D5,
            ["6"] = Key.D6,
            ["7"] = Key.D7,
            ["8"] = Key.D8,
            ["9"] = Key.D9,
            ["num0"] = Key.NumPad0,
            ["num1"] = Key.NumPad1,
            ["num2"] = Key.NumPad2,
            ["num3"] = Key.NumPad3,
            ["num4"] = Key.NumPad4,
            ["num5"] = Key.NumPad5,
            ["num6"] = Key.NumPad6,
            ["num7"] = Key.NumPad7,
            ["num8"] = Key.NumPad8,
            ["num9"] = Key.NumPad9,
            ["del"] = Key.Delete,
            ["delete"] = Key.Delete,
            ["esc"] = Key.Escape,
            ["escape"] = Key.Escape,
            ["enter"] = Key.Enter,
            ["return"] = Key.Enter,
            ["pgup"] = Key.PageUp,
            ["pageup"] = Key.PageUp,
            ["pgdown"] = Key.PageDown,
            ["pagedown"] = Key.PageDown,
            ["plus"] = Key.OemPlus,
            ["+"] = Key.OemPlus,
            ["minus"] = Key.OemMinus,
            ["-"] = Key.OemMinus,
            ["space"] = Key.Space,
        };

    public static bool TryResolveModifier(string token, out KeyModifiers modifier)
    {
        if (string.Equals(token, "ctrl", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "control", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "cmd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "command", StringComparison.OrdinalIgnoreCase))
        {
            modifier = KeyModifiers.Control;
            return true;
        }

        if (string.Equals(token, "shift", StringComparison.OrdinalIgnoreCase))
        {
            modifier = KeyModifiers.Shift;
            return true;
        }

        if (string.Equals(token, "alt", StringComparison.OrdinalIgnoreCase))
        {
            modifier = KeyModifiers.Alt;
            return true;
        }

        if (string.Equals(token, "meta", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "win", StringComparison.OrdinalIgnoreCase))
        {
            modifier = KeyModifiers.Meta;
            return true;
        }

        modifier = KeyModifiers.None;
        return false;
    }

    public static bool TryResolveKey(string token, out Key key)
    {
        if (KeyAliases.TryGetValue(token.Trim(), out key))
            return true;

        if (Enum.TryParse(token.Trim(), ignoreCase: true, out Key direct))
        {
            key = direct;
            return true;
        }

        key = Key.None;
        return false;
    }

    public static string ToDisplayKey(Key key) =>
        key switch
        {
            Key.D0 or Key.NumPad0 => "0",
            Key.D1 or Key.NumPad1 => "1",
            Key.D2 or Key.NumPad2 => "2",
            Key.D3 or Key.NumPad3 => "3",
            Key.D4 or Key.NumPad4 => "4",
            Key.D5 or Key.NumPad5 => "5",
            Key.D6 or Key.NumPad6 => "6",
            Key.D7 or Key.NumPad7 => "7",
            Key.D8 or Key.NumPad8 => "8",
            Key.D9 or Key.NumPad9 => "9",
            Key.Escape => "Esc",
            Key.Delete => "Del",
            Key.PageUp => "PgUp",
            Key.PageDown => "PgDown",
            Key.OemPlus or Key.Add => "+",
            Key.OemMinus or Key.Subtract => "-",
            _ => key.ToString(),
        };

    public static string ToNormalizedKey(Key key) =>
        key switch
        {
            Key.D0 or Key.NumPad0 => "0",
            Key.D1 or Key.NumPad1 => "1",
            Key.D2 or Key.NumPad2 => "2",
            Key.D3 or Key.NumPad3 => "3",
            Key.D4 or Key.NumPad4 => "4",
            Key.D5 or Key.NumPad5 => "5",
            Key.D6 or Key.NumPad6 => "6",
            Key.D7 or Key.NumPad7 => "7",
            Key.D8 or Key.NumPad8 => "8",
            Key.D9 or Key.NumPad9 => "9",
            Key.OemPlus or Key.Add => "Plus",
            Key.OemMinus or Key.Subtract => "Minus",
            _ => key.ToString(),
        };
}
