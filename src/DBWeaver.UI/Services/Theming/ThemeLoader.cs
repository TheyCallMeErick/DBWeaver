using System.Text.Json;
using DBWeaver.UI.Services.Localization;

namespace DBWeaver.UI.Services.Theming;

public enum ThemeLoadStatus
{
    NotFound,
    Loaded,
    InvalidJson,
    Error,
}

public sealed class ThemeLoadResult
{
    public ThemeLoadStatus Status { get; init; }
    public ThemeConfig? Config { get; init; }
    public string? Message { get; init; }
}

public static class ThemeLoader
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ThemeLoadResult LoadFromPath(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new ThemeLoadResult
                {
                    Status = ThemeLoadStatus.NotFound,
                    Message = string.Format(
                        L("themeLoader.status.notFoundWithPath", "Theme file not found: {0}"),
                        path
                    ),
                };
            }

            string json = File.ReadAllText(path);
            ThemeConfig? config = JsonSerializer.Deserialize<ThemeConfig>(json, JsonOpts);
            if (config is null)
            {
                return new ThemeLoadResult
                {
                    Status = ThemeLoadStatus.InvalidJson,
                    Message = L("themeLoader.status.deserializedNull", "Theme JSON deserialized to null."),
                };
            }

            return new ThemeLoadResult
            {
                Status = ThemeLoadStatus.Loaded,
                Config = config,
                Message = L("themeLoader.status.loaded", "Theme JSON loaded successfully."),
            };
        }
        catch (JsonException ex)
        {
            return new ThemeLoadResult
            {
                Status = ThemeLoadStatus.InvalidJson,
                Message = ex.Message,
            };
        }
        catch (Exception ex)
        {
            return new ThemeLoadResult
            {
                Status = ThemeLoadStatus.Error,
                Message = ex.Message,
            };
        }
    }

    public static string GetDefaultThemePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Themes", "user-theme.json");
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
