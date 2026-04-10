using Avalonia.Media;
using DBWeaver.UI.Services.Theming;
using Xunit;

namespace DBWeaver.Tests.Unit.Services;

public class ThemeTokenMapperTests
{
    [Fact]
    public void Map_BgFields_MapsToUnifiedBgTokens()
    {
        var cfg = new ThemeConfig
        {
            Colors = new ThemeColorsConfig { Bg0 = "#010203", Bg1 = "#040506" }
        };

        ThemeTokenMapResult result = ThemeTokenMapper.Map(cfg);

        Assert.True(result.TokenOverrides.ContainsKey("Bg0"));
        Assert.True(result.TokenOverrides.ContainsKey("Bg0Brush"));
        Assert.True(result.TokenOverrides.ContainsKey("Bg1"));
        Assert.True(result.TokenOverrides.ContainsKey("Bg1Brush"));
        Assert.IsType<SolidColorBrush>(result.TokenOverrides["Bg0Brush"]);
    }

    [Fact]
    public void Map_AccentTealField_MapsToAccentTealTokens()
    {
        var cfg = new ThemeConfig
        {
            Colors = new ThemeColorsConfig { AccentTeal = "#0D9488" }
        };

        ThemeTokenMapResult result = ThemeTokenMapper.Map(cfg);

        Assert.True(result.TokenOverrides.ContainsKey("AccentTeal"));
        Assert.True(result.TokenOverrides.ContainsKey("AccentTealBrush"));
        Assert.IsType<SolidColorBrush>(result.TokenOverrides["AccentTealBrush"]);
    }

    [Fact]
    public void Map_TypographyRoles_MapToCorrectTokenKeys()
    {
        var cfg = new ThemeConfig
        {
            Typography = new ThemeTypographyConfig
            {
                UiFont = "Segoe UI",
                NodeFont = "Space Grotesk",
                MonoFont = "JetBrainsMono Nerd Font",
                DisplaySize = 22,
                HeadingSize = 16,
                TitleSize = 15,
                NodeTitleSize = 14,
                LabelSize = 14,
                BodySize = 12,
                CaptionSize = 11,
                MonoBodySize = 12,
                MonoSmallSize = 11
            }
        };

        ThemeTokenMapResult result = ThemeTokenMapper.Map(cfg);

        Assert.True(result.TokenOverrides.ContainsKey("UIFont"));
        Assert.True(result.TokenOverrides.ContainsKey("NodeFont"));
        Assert.True(result.TokenOverrides.ContainsKey("MonoFont"));
        Assert.True(result.TokenOverrides.ContainsKey("FontSizeDisplay"));
        Assert.True(result.TokenOverrides.ContainsKey("FontSizeHeading"));
        Assert.True(result.TokenOverrides.ContainsKey("FontSizeTitle"));
        Assert.True(result.TokenOverrides.ContainsKey("FontSizeNodeTitle"));
        Assert.True(result.TokenOverrides.ContainsKey("FontSizeLabel"));
        Assert.True(result.TokenOverrides.ContainsKey("FontSizeBody"));
        Assert.True(result.TokenOverrides.ContainsKey("FontSizeCaption"));
        Assert.True(result.TokenOverrides.ContainsKey("FontSizeMonoBody"));
        Assert.True(result.TokenOverrides.ContainsKey("FontSizeMonoSmall"));
    }

    [Fact]
    public void Map_TextHierarchy_MapsToAllTextColorTokens()
    {
        var cfg = new ThemeConfig
        {
            Colors = new ThemeColorsConfig
            {
                TextPrimary = "#E7ECFF",
                TextSecondary = "#AEB9D9",
                TextMuted = "#7F8AAE",
                TextDisabled = "#66708F",
                TextInverse = "#0B0F1D",
                TextAccent = "#8FA7FF"
            }
        };

        ThemeTokenMapResult result = ThemeTokenMapper.Map(cfg);

        Assert.True(result.TokenOverrides.ContainsKey("TextPrimary"));
        Assert.True(result.TokenOverrides.ContainsKey("TextSecondary"));
        Assert.True(result.TokenOverrides.ContainsKey("TextMuted"));
        Assert.True(result.TokenOverrides.ContainsKey("TextDisabled"));
        Assert.True(result.TokenOverrides.ContainsKey("TextInverse"));
        Assert.True(result.TokenOverrides.ContainsKey("TextAccent"));
    }

    [Fact]
    public void Map_InvalidEntries_AreIgnoredWithWarnings()
    {
        var cfg = new ThemeConfig
        {
            Colors = new ThemeColorsConfig { Bg0 = "not-a-color" },
            Typography = new ThemeTypographyConfig { BodySize = 999 }
        };

        ThemeTokenMapResult result = ThemeTokenMapper.Map(cfg);

        Assert.False(result.TokenOverrides.ContainsKey("Bg0"));
        Assert.False(result.TokenOverrides.ContainsKey("FontSizeBody"));
        Assert.NotEmpty(result.Warnings);
    }
}
