using Avalonia.Input;
using DBWeaver.UI.Services.Input.ShortcutRegistry;

namespace DBWeaver.Tests.Unit.Services.Input.ShortcutRegistry;

public sealed class ShortcutGestureParserTests
{
    private readonly ShortcutGestureParser _parser = new();

    [Fact]
    public void Parse_CtrlShiftP_ReturnsNormalizedGesture()
    {
        ShortcutGestureParseResult result = _parser.Parse("Ctrl+Shift+P");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Gesture);
        Assert.Equal(Key.P, result.Gesture.Key);
        Assert.Equal(KeyModifiers.Control | KeyModifiers.Shift, result.Gesture.Modifiers);
        Assert.Equal("Ctrl+Shift+P", result.Gesture.NormalizedText);
    }

    [Fact]
    public void Parse_ModifierOrderVariants_ProduceSameNormalizedGesture()
    {
        ShortcutGestureParseResult first = _parser.Parse("Ctrl+Shift+P");
        ShortcutGestureParseResult second = _parser.Parse("shift+ctrl+p");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Gesture!.NormalizedText, second.Gesture!.NormalizedText);
    }

    [Theory]
    [InlineData("Del")]
    [InlineData("Delete")]
    public void Parse_DeleteAliases_ResolveToDeleteKey(string input)
    {
        ShortcutGestureParseResult result = _parser.Parse(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(Key.Delete, result.Gesture!.Key);
    }

    [Fact]
    public void Parse_UnknownKey_ReturnsUnknownKeyIssue()
    {
        ShortcutGestureParseResult result = _parser.Parse("Ctrl+Nope");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Issue);
        Assert.Equal(ShortcutValidationCodes.UnknownKey, result.Issue!.Code);
    }

    [Fact]
    public void Parse_InvalidModifierSequence_ReturnsInvalidFormat()
    {
        ShortcutGestureParseResult result = _parser.Parse("Ctrl+Shift+Alt");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Issue);
        Assert.Equal(ShortcutValidationCodes.InvalidFormat, result.Issue!.Code);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsDisallowedEmpty()
    {
        ShortcutGestureParseResult result = _parser.Parse(" ");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Issue);
        Assert.Equal(ShortcutValidationCodes.DisallowedEmpty, result.Issue!.Code);
    }

    [Fact]
    public void Parse_CtrlPlus_ResolvesToOemPlus()
    {
        ShortcutGestureParseResult result = _parser.Parse("Ctrl++");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Gesture);
        Assert.Equal(Key.OemPlus, result.Gesture.Key);
        Assert.Equal("Ctrl+Plus", result.Gesture.NormalizedText);
    }

    [Fact]
    public void Parse_CtrlZero_ResolvesToDigitZero()
    {
        ShortcutGestureParseResult result = _parser.Parse("Ctrl+0");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Gesture);
        Assert.Equal(Key.D0, result.Gesture.Key);
        Assert.Equal("Ctrl+0", result.Gesture.NormalizedText);
    }
}
