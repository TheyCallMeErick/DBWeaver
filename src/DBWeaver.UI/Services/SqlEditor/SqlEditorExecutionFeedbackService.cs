using DBWeaver.UI.Services.Localization;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorExecutionFeedbackService
{
    private readonly ILocalizationService _localization;

    public SqlEditorExecutionFeedbackService(ILocalizationService? localization = null)
    {
        _localization = localization ?? LocalizationService.Instance;
    }

    public SqlEditorExecutionFeedback Build(SqlEditorResultSet result)
    {
        if (result.Success)
        {
            return new SqlEditorExecutionFeedback
            {
                StatusText = L("sqlEditor.status.success", "Execution succeeded."),
                DetailText = string.Format(
                    L("sqlEditor.detail.rowsAndTime", "{0} row(s) in {1} ms."),
                    result.RowsAffected,
                    Math.Round(result.ExecutionTime.TotalMilliseconds)),
                HasError = false,
            };
        }

        if (string.Equals(result.ErrorMessage, L("sqlEditor.error.executionCanceled", "SQL execution was canceled."), StringComparison.Ordinal))
        {
            return new SqlEditorExecutionFeedback
            {
                StatusText = L("sqlEditor.status.canceled", "Execution canceled."),
                DetailText = result.ErrorMessage,
                HasError = false,
            };
        }

        return new SqlEditorExecutionFeedback
        {
            StatusText = L("sqlEditor.status.failed", "Execution failed."),
            DetailText = result.ErrorMessage,
            HasError = true,
        };
    }

    private string L(string key, string fallback)
    {
        string value = _localization[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}

