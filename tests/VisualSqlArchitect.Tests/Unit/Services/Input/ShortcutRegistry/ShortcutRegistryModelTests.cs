using Avalonia.Input;
using DBWeaver.UI.Services.Input.ShortcutRegistry;

namespace DBWeaver.Tests.Unit.Services.Input.ShortcutRegistry;

public sealed class ShortcutRegistryModelTests
{
    [Fact]
    public void ShortcutActionId_RequiresNonWhitespaceValue()
    {
        Assert.Throws<ArgumentException>(() => new ShortcutActionId(""));
        Assert.Throws<ArgumentException>(() => new ShortcutActionId(" "));
    }

    [Fact]
    public void ShortcutGesture_NormalizesUnsupportedModifierBits()
    {
        var gesture = new ShortcutGesture(
            Key.P,
            KeyModifiers.Control | KeyModifiers.Shift | (KeyModifiers)1024,
            "Ctrl+Shift+P",
            "Ctrl+Shift+P");

        Assert.Equal(KeyModifiers.Control | KeyModifiers.Shift, gesture.Modifiers);
    }

    [Fact]
    public void ShortcutGesture_HasValueSemantics()
    {
        var first = new ShortcutGesture(Key.F1, KeyModifiers.None, "F1", "F1");
        var second = new ShortcutGesture(Key.F1, KeyModifiers.None, "F1", "F1");

        Assert.Equal(first, second);
    }
}
