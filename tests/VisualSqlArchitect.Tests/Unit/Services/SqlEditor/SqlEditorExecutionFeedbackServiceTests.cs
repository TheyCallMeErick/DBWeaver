using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.Services.SqlEditor;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlEditorExecutionFeedbackServiceTests
{
    [Fact]
    public void Build_WhenSuccess_ReturnsSuccessFeedback()
    {
        var sut = new SqlEditorExecutionFeedbackService();
        var result = new SqlEditorResultSet
        {
            StatementSql = "SELECT 1",
            Success = true,
            RowsAffected = 3,
            ExecutionTime = TimeSpan.FromMilliseconds(5),
            ExecutedAt = DateTimeOffset.UtcNow,
        };

        SqlEditorExecutionFeedback feedback = sut.Build(result);

        AssertLocalized(feedback.StatusText, "Execucao concluida com sucesso.", "Execution succeeded.");
        Assert.Contains("3", feedback.DetailText);
        Assert.False(feedback.HasError);
    }

    [Fact]
    public void Build_WhenCanceled_ReturnsCanceledFeedback()
    {
        var sut = new SqlEditorExecutionFeedbackService();
        var result = new SqlEditorResultSet
        {
            StatementSql = "SELECT 1",
            Success = false,
            ErrorMessage = "A execucao SQL foi cancelada.",
            ExecutedAt = DateTimeOffset.UtcNow,
        };

        SqlEditorExecutionFeedback feedback = sut.Build(result);

        AssertLocalized(feedback.StatusText, "Execucao cancelada.", "Execution canceled.");
        AssertLocalized(feedback.DetailText, "A execucao SQL foi cancelada.", "SQL execution was canceled.");
        Assert.False(feedback.HasError);
    }

    [Fact]
    public void Build_WhenFailure_ReturnsErrorFeedback()
    {
        var sut = new SqlEditorExecutionFeedbackService();
        var result = new SqlEditorResultSet
        {
            StatementSql = "SELECT 1",
            Success = false,
            ErrorMessage = "broken",
            ExecutedAt = DateTimeOffset.UtcNow,
        };

        SqlEditorExecutionFeedback feedback = sut.Build(result);

        AssertLocalized(feedback.StatusText, "Falha na execucao.", "Execution failed.");
        Assert.Equal("broken", feedback.DetailText);
        Assert.True(feedback.HasError);
    }

    [Fact]
    public void Build_WhenLocalized_UsesLocalizedValues()
    {
        var sut = new SqlEditorExecutionFeedbackService(new FakeLocalizationService(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sqlEditor.status.failed"] = "Falhou",
        }));
        var result = new SqlEditorResultSet
        {
            StatementSql = "SELECT 1",
            Success = false,
            ErrorMessage = "x",
            ExecutedAt = DateTimeOffset.UtcNow,
        };

        SqlEditorExecutionFeedback feedback = sut.Build(result);

        Assert.Equal("Falhou", feedback.StatusText);
    }

    private sealed class FakeLocalizationService(Dictionary<string, string> values) : ILocalizationService
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public string CurrentCulture => "en-US";
        public string CurrentLanguageLabel => "EN-US";
        public string this[string key] => values.TryGetValue(key, out string? value) ? value : key;
        public bool ToggleCulture() => false;
        public bool SetCulture(string culture) => false;
    }

    private static void AssertLocalized(string? actual, params string[] expectedValues)
    {
        Assert.NotNull(actual);
        Assert.Contains(actual!, expectedValues);
    }
}
