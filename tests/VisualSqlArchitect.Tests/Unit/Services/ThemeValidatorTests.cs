using DBWeaver.UI.Services.Theming;
using Xunit;

namespace DBWeaver.Tests.Unit.Services;

public class ThemeValidatorTests
{
    [Fact]
    public void Validate_NullConfig_ReturnsError()
    {
        ThemeValidationResult result = ThemeValidator.Validate(null);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, e => Assert.False(string.IsNullOrWhiteSpace(e)));
    }

    [Fact]
    public void Validate_InvalidColor_AddsWarningButRemainsValid()
    {
        var cfg = new ThemeConfig
        {
            Colors = new ThemeColorsConfig
            {
                Bg0 = "not-a-color",
                TextAccent = "also-not-a-color"
            }
        };

        ThemeValidationResult result = ThemeValidator.Validate(cfg);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("bg0", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Warnings, w => w.Contains("textaccent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_InvalidTypographySizes_AddsWarnings()
    {
        var cfg = new ThemeConfig
        {
            Typography = new ThemeTypographyConfig
            {
                NodeTitleSize = 7,
                MonoSmallSize = 100
            }
        };

        ThemeValidationResult result = ThemeValidator.Validate(cfg);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("nodetitlesize", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Warnings, w => w.Contains("monosmallsize", StringComparison.OrdinalIgnoreCase));
    }
}
