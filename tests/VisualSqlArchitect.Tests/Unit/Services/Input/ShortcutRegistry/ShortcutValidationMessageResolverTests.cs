using DBWeaver.UI.Services.Input.ShortcutRegistry;

namespace DBWeaver.Tests.Unit.Services.Input.ShortcutRegistry;

public sealed class ShortcutValidationMessageResolverTests
{
    [Fact]
    public void Resolve_InvalidFormat_ReturnsStandardizedMessage()
    {
        var issue = new ShortcutValidationIssue(
            ShortcutValidationCodes.InvalidFormat,
            "raw parser message",
            ShortcutActionIds.OpenCommandPalette,
            "Ctrl+Nope");

        string resolved = ShortcutValidationMessageResolver.Resolve(issue);

        Assert.Contains("format is invalid", resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_DuplicateGesture_ReturnsStandardizedMessage()
    {
        string resolved = ShortcutValidationMessageResolver.Resolve(
            ShortcutValidationCodes.DuplicateGesture,
            "Gesture conflict");

        Assert.Contains("already in use", resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_UnknownCode_FallsBackToProvidedMessage()
    {
        string resolved = ShortcutValidationMessageResolver.Resolve("shortcut.custom_code", "Custom fallback.");

        Assert.Equal("Custom fallback.", resolved);
    }

    [Fact]
    public void Resolve_NullIssue_ReturnsGenericFailureMessage()
    {
        string resolved = ShortcutValidationMessageResolver.Resolve(issue: null);

        Assert.Contains("Unable to update shortcut", resolved, StringComparison.OrdinalIgnoreCase);
    }
}

