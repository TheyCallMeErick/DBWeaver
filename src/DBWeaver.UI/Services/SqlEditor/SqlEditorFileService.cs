using System.IO;
using DBWeaver.UI.Services.Localization;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorFileService
{
    private readonly ILocalizationService _localization;

    public SqlEditorFileService(ILocalizationService? localization = null)
    {
        _localization = localization ?? LocalizationService.Instance;
    }

    public async Task<SqlEditorFileSaveOutcome> SaveAsync(string sql, string? filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new SqlEditorFileSaveOutcome
            {
                Success = false,
                StatusText = L("sqlEditor.file.save.canceled", "Save canceled."),
                DetailText = L("sqlEditor.file.save.noPath", "No target path selected."),
                HasError = false,
            };
        }

        try
        {
            await File.WriteAllTextAsync(filePath, sql, ct);
            return new SqlEditorFileSaveOutcome
            {
                Success = true,
                StatusText = L("sqlEditor.file.save.success", "SQL file saved."),
                DetailText = filePath,
                HasError = false,
            };
        }
        catch (Exception ex)
        {
            return new SqlEditorFileSaveOutcome
            {
                Success = false,
                StatusText = L("sqlEditor.file.save.failed", "Save failed."),
                DetailText = ex.Message,
                HasError = true,
            };
        }
    }

    public async Task<SqlEditorFileOpenOutcome> OpenAsync(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new SqlEditorFileOpenOutcome
            {
                Success = false,
                StatusText = L("sqlEditor.file.open.failed", "Open failed."),
                DetailText = L("sqlEditor.file.open.notFound", "Selected SQL file was not found."),
                HasError = true,
                Content = null,
            };
        }

        try
        {
            string content = await File.ReadAllTextAsync(filePath, ct);
            return new SqlEditorFileOpenOutcome
            {
                Success = true,
                StatusText = L("sqlEditor.file.open.success", "SQL file opened."),
                DetailText = filePath,
                HasError = false,
                Content = content,
            };
        }
        catch (Exception ex)
        {
            return new SqlEditorFileOpenOutcome
            {
                Success = false,
                StatusText = L("sqlEditor.file.open.failed", "Open failed."),
                DetailText = ex.Message,
                HasError = true,
                Content = null,
            };
        }
    }

    private string L(string key, string fallback)
    {
        string value = _localization[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}

