using System.Text.Json;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.Services.Settings;

public sealed class ThemeJsonOperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int AppliedTokenCount { get; init; }
    public List<string> Warnings { get; init; } = [];
}

public sealed class ThemeJsonSettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    private readonly Func<string> _themePathProvider;

    public ThemeJsonSettingsService(Func<string>? themePathProvider = null)
    {
        _themePathProvider = themePathProvider ?? ThemeLoader.GetDefaultThemePath;
    }

    public string GetEditorJsonOrTemplate()
    {
        string path = _themePathProvider();
        if (File.Exists(path))
            return File.ReadAllText(path);

        return L(
            "themeJson.editor.template",
            $$"""
{
  "meta": { "name": "Custom Theme" },
  "colors": {
        "bg0": "{{UiColorConstants.C_0B1220}}",
        "textPrimary": "{{UiColorConstants.C_E8EAED}}",
        "textSecondary": "{{UiColorConstants.C_8B95A8}}",
        "textMuted": "#7F8AAE",
        "textDisabled": "#66708F",
        "textInverse": "#0B0F1D",
        "textAccent": "#8FA7FF"
    },
    "typography": {
        "uiFont": "Manrope,Segoe UI,Arial,sans-serif",
        "nodeFont": "Space Grotesk,Manrope,Segoe UI,Arial,sans-serif",
        "monoFont": "JetBrainsMono Nerd Font,JetBrains Mono,Cascadia Code,Consolas,monospace",
        "displaySize": 24,
        "headingSize": 18,
        "titleSize": 15,
        "nodeTitleSize": 14,
        "labelSize": 13,
        "bodySize": 12,
        "captionSize": 11,
        "monoBodySize": 12,
        "monoSmallSize": 11
  }
}
"""
        );
    }

    public ThemeJsonOperationResult ApplyAndPersist(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return new ThemeJsonOperationResult
            {
                Success = false,
                Message = L("themeJson.error.pasteBeforeApply", "Cole um JSON de tema antes de aplicar."),
            };
        }

        ThemeConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<ThemeConfig>(rawJson, JsonOpts);
        }
        catch (JsonException ex)
        {
            return new ThemeJsonOperationResult
            {
                Success = false,
                Message = string.Format(L("themeJson.error.invalidJson", "JSON invalido: {0}"), ex.Message),
            };
        }

        if (config is null)
        {
            return new ThemeJsonOperationResult
            {
                Success = false,
                Message = L("themeJson.error.emptyPayload", "JSON invalido: payload vazio."),
            };
        }

        ThemeValidationResult validation = ThemeValidator.Validate(config);
        if (!validation.IsValid)
        {
            return new ThemeJsonOperationResult
            {
                Success = false,
                Message = string.Format(
                    L("themeJson.error.invalidTheme", "Tema invalido: {0}"),
                    string.Join(" | ", validation.Errors)
                ),
            };
        }

        ThemeTokenMapResult mapped = ThemeTokenMapper.Map(config);
        int applied = ThemeRuntimeApplier.ApplyToCurrentApplication(mapped.TokenOverrides);

        try
        {
            string path = _themePathProvider();
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, rawJson);
        }
        catch (Exception ex)
        {
            return new ThemeJsonOperationResult
            {
                Success = false,
                Message = string.Format(
                    L("themeJson.error.appliedButSaveFailed", "Tema aplicado, mas falhou ao salvar: {0}"),
                    ex.Message
                ),
                AppliedTokenCount = applied,
                Warnings = mapped.Warnings,
            };
        }

        return new ThemeJsonOperationResult
        {
            Success = true,
            Message = L("themeJson.success.appliedAndSaved", "Tema JSON aplicado e salvo."),
            AppliedTokenCount = applied,
            Warnings = mapped.Warnings,
        };
    }

    public ThemeJsonOperationResult RestoreDefault()
    {
        try
        {
            string path = _themePathProvider();
            if (File.Exists(path))
                File.Delete(path);

            return new ThemeJsonOperationResult
            {
                Success = true,
                Message = L(
                    "themeJson.success.customRemoved",
                    "Tema personalizado removido. Reinicie o app para voltar totalmente ao tema padrao."
                ),
            };
        }
        catch (Exception ex)
        {
            return new ThemeJsonOperationResult
            {
                Success = false,
                Message = string.Format(
                    L("themeJson.error.restoreDefaultFailed", "Falha ao restaurar tema padrao: {0}"),
                    ex.Message
                ),
            };
        }
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
