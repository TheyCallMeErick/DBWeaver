using System.IO;

namespace DBWeaver.Tests.Unit.Controls;

public sealed class SqlEditorRightSidebarControlTemplateRegressionTests
{
    [Fact]
    public void SqlEditorRightSidebarTemplate_BindsTelemetryAndHistory()
    {
        string xaml = ReadXaml();

        Assert.Contains("x:DataType=\"vm:SqlEditorViewModel\"", xaml);
        Assert.Contains("Text=\"{Binding LastExecutionMessage}\"", xaml);
        Assert.Contains("Text=\"{Binding ExecutionDetailText}\"", xaml);
        Assert.Contains("Text=\"{Binding ResultSummaryText}\"", xaml);
        Assert.Contains("Text=\"{Binding ExecutionTelemetryText}\"", xaml);
        Assert.Contains("Text=\"{Binding CompletionTelemetryText}\"", xaml);
        Assert.Contains("Text=\"{Binding ExecutionTelemetryErrorsText}\"", xaml);
        Assert.Contains("Text=\"{Binding HistoryEmptyText}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding IsExecutionHistoryEmpty}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding HasExecutionHistory}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding FilteredExecutionHistory}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding HasFilteredExecutionHistory}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding HasHistorySearchNoResults}\"", xaml);
        Assert.Contains("Text=\"{Binding ExecutedAt}\"", xaml);
        Assert.Contains("Text=\"{Binding Sql}\"", xaml);
        Assert.Contains("Click=\"ExplainButton_Click\"", xaml);
        Assert.Contains("Click=\"BenchmarkButton_Click\"", xaml);
        Assert.Contains("Text=\"{Binding ExplainSummaryText}\"", xaml);
        Assert.Contains("Text=\"{Binding ExplainRawOutput}\"", xaml);
        Assert.Contains("Text=\"{Binding BenchmarkProgressText}\"", xaml);
        Assert.Contains("Text=\"{Binding BenchmarkSummaryText}\"", xaml);
    }

    private static string ReadXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "DBWeaver.UI",
                "Controls",
                "SqlEditor",
                "SqlEditorRightSidebarControl.axaml");

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate SqlEditorRightSidebarControl.axaml from test base directory.");
    }
}
