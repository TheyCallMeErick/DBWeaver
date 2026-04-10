using Avalonia.Media;

namespace DBWeaver.UI.Services.Theming;

public sealed class ThemeTokenMapResult
{
    public Dictionary<string, object> TokenOverrides { get; } = new(StringComparer.Ordinal);
    public List<string> Warnings { get; } = [];
}

public static class ThemeTokenMapper
{
    public static ThemeTokenMapResult Map(ThemeConfig config)
    {
        var result = new ThemeTokenMapResult();

        if (config.Colors is not null)
        {
            MapColor(config.Colors.Bg0, "Bg0", "Bg0Brush", result);
            MapColor(config.Colors.Bg1, "Bg1", "Bg1Brush", result);
            MapColor(config.Colors.Bg2, "Bg2", "Bg2Brush", result);
            MapColor(config.Colors.Bg3, "Bg3", "Bg3Brush", result);
            MapColor(config.Colors.Bg4, "Bg4", "Bg4Brush", result);
            MapColor(config.Colors.AccentTeal, "AccentTeal", "AccentTealBrush", result);
            MapColor(config.Colors.TextPrimary, "TextPrimary", "TextPrimaryBrush", result);
            MapColor(config.Colors.TextSecondary, "TextSecondary", "TextSecondaryBrush", result);
            MapColor(config.Colors.TextMuted, "TextMuted", "TextMutedBrush", result);
            MapColor(config.Colors.TextDisabled, "TextDisabled", "TextDisabledBrush", result);
            MapColor(config.Colors.TextInverse, "TextInverse", "TextInverseBrush", result);
            MapColor(config.Colors.TextAccent, "TextAccent", "TextAccentBrush", result);
            MapColor(config.Colors.BtnPrimaryBg, "BtnPrimaryBg", "BtnPrimaryBgBrush", result);
            MapColor(config.Colors.BtnPrimaryFg, "BtnPrimaryFg", "BtnPrimaryFgBrush", result);
            MapColor(config.Colors.BtnWarningBg, "BtnWarningBg", "BtnWarningBgBrush", result);
            MapColor(config.Colors.BtnWarningFg, "BtnWarningFg", "BtnWarningFgBrush", result);
        }

        if (config.Typography is not null)
        {
            MapFont(config.Typography.UiFont, "UIFont", result);
            MapFont(config.Typography.NodeFont, "NodeFont", result);
            MapFont(config.Typography.MonoFont, "MonoFont", result);
            MapSize(config.Typography.DisplaySize, "FontSizeDisplay", result);
            MapSize(config.Typography.HeadingSize, "FontSizeHeading", result);
            MapSize(config.Typography.TitleSize, "FontSizeTitle", result);
            MapSize(config.Typography.NodeTitleSize, "FontSizeNodeTitle", result);
            MapSize(config.Typography.LabelSize, "FontSizeLabel", result);
            MapSize(config.Typography.BodySize, "FontSizeBody", result);
            MapSize(config.Typography.CaptionSize, "FontSizeCaption", result);
            MapSize(config.Typography.MonoBodySize, "FontSizeMonoBody", result);
            MapSize(config.Typography.MonoSmallSize, "FontSizeMonoSmall", result);
        }

        return result;
    }

    private static void MapColor(string? value, string colorKey, string brushKey, ThemeTokenMapResult result)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (!Color.TryParse(value, out Color color))
        {
            result.Warnings.Add($"Invalid color '{value}' for {colorKey}; ignored.");
            return;
        }

        result.TokenOverrides[colorKey] = color;
        result.TokenOverrides[brushKey] = new SolidColorBrush(color);
    }

    private static void MapFont(string? value, string key, ThemeTokenMapResult result)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        result.TokenOverrides[key] = new FontFamily(value);
    }

    private static void MapSize(double? value, string key, ThemeTokenMapResult result)
    {
        if (value is null)
            return;

        if (value < 8 || value > 48)
        {
            result.Warnings.Add($"Invalid size {value} for {key}; expected 8..48.");
            return;
        }

        result.TokenOverrides[key] = value.Value;
    }
}
