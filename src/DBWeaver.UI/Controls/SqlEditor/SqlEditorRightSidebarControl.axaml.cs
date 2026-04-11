using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Controls.SqlEditor;

public partial class SqlEditorRightSidebarControl : UserControl
{
    public SqlEditorRightSidebarControl()
    {
        InitializeComponent();
    }

    private void HistoryRunButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SqlEditorViewModel vm)
            return;

        if (sender is not Button button || button.DataContext is not SqlEditorHistoryEntry entry)
            return;

        _ = vm.ExecuteHistoryEntryAsync(entry);
    }

    private void HistoryUseButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SqlEditorViewModel vm)
            return;

        if (sender is not Button button || button.DataContext is not SqlEditorHistoryEntry entry)
            return;

        vm.UseHistoryEntryInEditor(entry);
    }

    private async void HistoryCopyButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SqlEditorViewModel vm)
            return;

        if (sender is not Button button || button.DataContext is not SqlEditorHistoryEntry entry)
            return;

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
            return;

        await topLevel.Clipboard.SetTextAsync(entry.Sql ?? string.Empty);
        vm.PublishStatus(L("sqlEditor.history.copiedFromHistory", "SQL copiado do historico."));
    }

    private void ClearHistoryButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SqlEditorViewModel vm)
            return;

        vm.RequestClearExecutionHistory();
    }

    private void ClearHistorySearchButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SqlEditorViewModel vm)
            return;

        vm.HistorySearchText = string.Empty;
    }

    private void HistorySearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SqlEditorViewModel vm)
            return;

        if (e.Key == Key.Down)
        {
            vm.SelectNextHistoryEntry();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            vm.SelectPreviousHistoryEntry();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            vm.HistorySearchText = string.Empty;
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
            return;

        _ = vm.ExecuteSelectedHistoryEntryAsync();
        e.Handled = true;
    }

    private void ExplainButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SqlEditorViewModel vm)
            return;

        _ = vm.RunExplainAsync(includeAnalyze: false);
    }

    private void BenchmarkButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SqlEditorViewModel vm)
            return;

        _ = vm.RunBenchmarkAsync();
    }

    private void BenchmarkCancelButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SqlEditorViewModel vm)
            return;

        vm.CancelBenchmark();
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
