using System.IO;

namespace DBWeaver.Tests.Unit.Controls;

public sealed class SqlEditorControlTemplateRegressionTests
{
    [Fact]
    public void SqlEditorTemplate_UsesAvaloniaEditWithActiveTabBinding()
    {
        string xaml = ReadSqlEditorControlXaml();

        Assert.Contains("x:Name=\"SqlTextEditor\"", xaml);
        Assert.Contains("ShowLineNumbers=\"True\"", xaml);
        Assert.Contains("x:DataType=\"vm:SqlEditorViewModel\"", xaml);
        Assert.Contains("<ctrl:SqlEditorTabBar", xaml);
        Assert.Contains("<ctrl:SqlEditorResultPanel", xaml);
        Assert.Contains("IsReadOnly=\"False\"", xaml);
        Assert.Contains("IsVisible=\"{Binding ShouldShowResultsSheet}\"", xaml);
        Assert.Contains("Height=\"{Binding ResultsSheetHeight}\"", xaml);
        Assert.Contains("<GridSplitter Grid.Row=\"0\"", xaml);
        Assert.Contains("x:Name=\"ResultsResizeGrip\"", xaml);
        Assert.Contains("Command=\"{Binding CloseResultsSheetCommand}\"", xaml);
        Assert.Contains("<ctrl:MutationConfirmDialog", xaml);
        Assert.Contains("Text=\"{Binding ExecutionStatusText}\"", xaml);
        Assert.Contains("Text=\"{Binding ResultSummaryText}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding IsExecuting}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding HasPendingMutationConfirmation}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding HasPendingCloseTabConfirmation}\"", xaml);
        Assert.Contains("sqlEditor.tab.closeAnyway", xaml);
        Assert.Contains("sqlEditor.tab.keepTab", xaml);
        Assert.Contains("Command=\"{Binding ConfirmPendingCloseTabCommand}\"", xaml);
        Assert.Contains("Command=\"{Binding CancelPendingCloseTabCommand}\"", xaml);
    }

    [Fact]
    public void SqlEditorControlCodeBehind_WiresF8AndCtrlEnterExecutionShortcuts()
    {
        string source = ReadSqlEditorControlCodeBehind();

        Assert.Contains("Key.F8", source);
        Assert.Contains("Key.F5", source);
        Assert.Contains("Key.F4", source);
        Assert.Contains("Key.F6", source);
        Assert.Contains("Key.S", source);
        Assert.Contains("Key.O", source);
        Assert.Contains("Key.T", source);
        Assert.Contains("Key.W", source);
        Assert.Contains("Key.Space", source);
        Assert.Contains("Key.D1", source);
        Assert.Contains("Key.D9", source);
        Assert.Contains("NumPad1", source);
        Assert.Contains("KeyModifiers.Control", source);
        Assert.Contains("GetCompletionRequest", source);
        Assert.Contains("CompletionWindow", source);
        Assert.Contains("TextEntered", source);
        Assert.Contains("ShouldAutoTriggerCompletionAfterSpace", source);
        Assert.Contains("LargeEditorCompletionThreshold = 10_000", source);
        Assert.Contains("Editor grande — completion sob demanda (Ctrl+Space).", source);
        Assert.Contains("_completionWindow.CompletionList.CompletionData.Clear();", source);
        Assert.Contains("_vm.RecordCompletionLatency(stopwatch.Elapsed);", source);
        Assert.Contains("AllowScrollBelowDocument = true", source);
        Assert.Contains("ApplyEditorExecutionState", source);
        Assert.Contains("nameof(SqlEditorViewModel.IsExecuting)", source);
        Assert.Contains("StandardCursorType.Wait", source);
        Assert.Contains("ExecuteAllAsync", source);
        Assert.Contains("ExecuteSelectionOrCurrentAsync", source);
        Assert.Contains("SaveActiveTabAsync", source);
        Assert.Contains("OpenSqlFileAsync", source);
        Assert.Contains("CloseActiveTabCommand", source);
        Assert.Contains("NewTabCommand", source);
        Assert.Contains("ActiveEditorTabIndex", source);
        Assert.Contains("CancelExecution", source);
        Assert.Contains("RunExplainAsync", source);
        Assert.Contains("RunBenchmarkAsync", source);
        Assert.Contains("SqlExecutionStatementHighlightRenderer", source);
        Assert.Contains("RefreshExecutionStatementHighlight", source);
    }

    private static string ReadSqlEditorControlXaml()
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
                "SqlEditorControl.axaml"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate SqlEditorControl.axaml from test base directory.");
    }

    private static string ReadSqlEditorControlCodeBehind()
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
                "SqlEditorControl.axaml.cs"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate SqlEditorControl.axaml.cs from test base directory.");
    }
}
