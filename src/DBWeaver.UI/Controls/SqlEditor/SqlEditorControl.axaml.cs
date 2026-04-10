using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Avalonia.Input;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using DBWeaver.UI.Services.SqlEditor.Reports;
using DBWeaver.UI.Services;
using DBWeaver.UI.Services.SqlEditor;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.Controls.SqlEditor;

[ExcludeFromCodeCoverage]
public partial class SqlEditorControl : UserControl
{
    private TextEditor? _editor;
    private SqlEditorViewModel? _vm;
    private CompletionWindow? _completionWindow;
    private readonly SqlEditorReportExportService _reportExportService = new();

    public SqlEditorControl()
    {
        InitializeComponent();
        ConfigureTextEditor();
        DataContextChanged += (_, _) => AttachViewModel();
        AttachedToVisualTree += (_, _) => FocusEditor();
        Loaded += (_, _) => EnsureEditorReady();
    }

    private void ConfigureTextEditor()
    {
        _editor = this.FindControl<TextEditor>("SqlTextEditor");
        if (_editor is null)
            return;

        _editor.Options.EnableHyperlinks = false;
        _editor.Options.EnableEmailHyperlinks = false;
        _editor.Options.HighlightCurrentLine = true;
        _editor.Options.AllowScrollBelowDocument = false;
        _editor.Options.IndentationSize = 2;
        _editor.Options.ConvertTabsToSpaces = true;
        _editor.IsReadOnly = false;
        _editor.Focusable = true;
        _editor.IsHitTestVisible = true;
        _editor.Cursor = new Cursor(StandardCursorType.Ibeam);
        _editor.SyntaxHighlighting = SqlEditorHighlightingService.GetSqlDefinition();
        EnsureEditorDocument();
        _editor.TextChanged += OnEditorTextChanged;
        _editor.TextArea.TextEntered += OnEditorTextEntered;
        _editor.PointerPressed += (_, _) => FocusEditor();
    }

    private void AttachViewModel()
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;

        _vm = DataContext as SqlEditorViewModel;
        if (_vm is not null)
            _vm.PropertyChanged += OnViewModelPropertyChanged;

        SyncEditorTextFromViewModel();
        FocusEditor();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SqlEditorViewModel.ActiveTab))
            SyncEditorTextFromViewModel();
    }

    private void SyncEditorTextFromViewModel()
    {
        if (_editor is null || _vm is null)
            return;

        EnsureEditorDocument();
        string text = _vm.ActiveTab.SqlText;
        bool editorDocumentMissing = _editor.Document is null;
        if (!editorDocumentMissing && string.Equals(_editor.Text, text, StringComparison.Ordinal))
            return;

        _editor.Text = text ?? string.Empty;
        EnsureEditorReady();
    }

    private void EnsureEditorDocument()
    {
        if (_editor is null)
            return;

        _editor.Document ??= new TextDocument(string.Empty);
    }

    private void EnsureEditorReady()
    {
        if (_editor is null)
            return;

        EnsureEditorDocument();
        if (_editor.TextArea is null)
            return;

        _editor.TextArea.IsEnabled = true;
        _editor.TextArea.Caret.BringCaretToView();
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_editor is null || _vm is null)
            return;

        string updated = _editor.Text ?? string.Empty;
        if (string.Equals(_vm.ActiveTab.SqlText, updated, StringComparison.Ordinal))
            return;

        _vm.ActiveTab.SqlText = updated;
        _vm.ActiveTab.IsDirty = true;
    }

    private void OnEditorTextEntered(object? sender, TextInputEventArgs e)
    {
        if (_editor is null || _vm is null || _completionWindow is not null)
            return;

        if (string.Equals(e.Text, ".", StringComparison.Ordinal))
        {
            ShowCompletionWindow();
            return;
        }

        if (string.Equals(e.Text, " ", StringComparison.Ordinal) && ShouldAutoTriggerCompletionAfterSpace())
            ShowCompletionWindow();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_vm is null || _editor is null || e.Handled)
            return;

        bool isExecuteSelection = e.Key == Key.F8;
        bool isExecuteAll = e.Key == Key.F5;
        bool isCompletion = e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool isSave = e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool isOpen = e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool isNewTab = e.Key == Key.T && e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool isCloseTab = e.Key == Key.W && e.KeyModifiers.HasFlag(KeyModifiers.Control);
        int? tabShortcutIndex = ResolveTabShortcutIndex(e.Key, e.KeyModifiers);
        bool isExecuteCurrent = (e.Key is Key.Enter or Key.Return) && e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool isCancel = e.Key == Key.Escape;

        if (isCancel)
        {
            _vm.CancelExecution();
            e.Handled = true;
            return;
        }

        if (isExecuteAll)
        {
            _ = ExecuteAllWithToastAsync();
            e.Handled = true;
            return;
        }

        if (isSave)
        {
            _ = SaveSqlFileFromPickerAsync();
            e.Handled = true;
            return;
        }

        if (isOpen)
        {
            _ = OpenSqlFileFromPickerAsync();
            e.Handled = true;
            return;
        }

        if (isCompletion)
        {
            ShowCompletionWindow();
            e.Handled = true;
            return;
        }

        if (isNewTab)
        {
            if (_vm.NewTabCommand.CanExecute(null))
                _vm.NewTabCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (isCloseTab)
        {
            if (_vm.CloseActiveTabCommand.CanExecute(null))
                _vm.CloseActiveTabCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (tabShortcutIndex is int targetIndex)
        {
            if (targetIndex < _vm.EditorTabs.Count)
                _vm.ActiveEditorTabIndex = targetIndex;
            e.Handled = true;
            return;
        }

        if (!isExecuteSelection && !isExecuteCurrent)
            return;

        _ = ExecuteSelectionOrCurrentWithToastAsync();
        e.Handled = true;
    }

    private async Task ExecuteSelectionOrCurrentWithToastAsync()
    {
        if (_vm is null || _editor is null)
            return;

        SqlEditorResultSet result = await _vm.ExecuteSelectionOrCurrentAsync(_editor.SelectionStart, _editor.SelectionLength, _editor.CaretOffset);
        ShowToastForResult(result);
    }

    private async Task ExecuteAllWithToastAsync()
    {
        if (_vm is null)
            return;

        IReadOnlyList<SqlEditorResultSet> results = await _vm.ExecuteAllAsync();
        if (results.Count == 0)
            return;

        int failures = results.Count(static r => !r.Success);
        if (failures == 0)
        {
            ShowShellToastSuccess("Consulta executada.", $"{results.Count} statement(s) executado(s) com sucesso.");
            return;
        }

        ShowShellToastWarning("Consulta executada com falhas.", $"{failures} de {results.Count} statement(s) falharam.");
    }

    private void ShowToastForResult(SqlEditorResultSet result)
    {
        if (!result.Success)
        {
            ShowShellToastError("Falha na execucao da consulta.", result.ErrorMessage);
            return;
        }

        long elapsedMs = (long)Math.Round(result.ExecutionTime.TotalMilliseconds);
        long rows = result.Data?.Rows.Count ?? result.RowsAffected ?? 0;
        string details = $"Rows: {rows}    Time: {elapsedMs} ms";
        ShowShellToastSuccess("Execucao concluida com sucesso.", details);
    }

    private void ShowShellToastSuccess(string message, string? details)
    {
        if (TopLevel.GetTopLevel(this) is Window window && window.DataContext is ShellViewModel shell)
            shell.Toasts.ShowSuccess(message, details);
    }

    private void ShowShellToastWarning(string message, string? details)
    {
        if (TopLevel.GetTopLevel(this) is Window window && window.DataContext is ShellViewModel shell)
            shell.Toasts.ShowWarning(message, details);
    }

    private void ShowShellToastError(string message, string? details)
    {
        if (TopLevel.GetTopLevel(this) is Window window && window.DataContext is ShellViewModel shell)
            shell.Toasts.ShowError(message, details);
    }

    private async Task OpenSqlFileFromPickerAsync()
    {
        if (_vm is null)
            return;

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
            return;

        var sqlFileType = new FilePickerFileType("SQL Files")
        {
            Patterns = ["*.sql", "*.txt"],
            MimeTypes = ["text/plain"],
        };

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open SQL File",
                AllowMultiple = false,
                FileTypeFilter = [sqlFileType, FilePickerFileTypes.All],
            });

        string? path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        await _vm.OpenSqlFileAsync(path);
    }

    private async Task SaveSqlFileFromPickerAsync()
    {
        if (_vm is null)
            return;

        if (!string.IsNullOrWhiteSpace(_vm.ActiveTab.FilePath))
        {
            await _vm.SaveActiveTabAsync(_vm.ActiveTab.FilePath);
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
            return;

        var sqlFileType = new FilePickerFileType(L("sqlEditor.saveSql.fileType", "SQL Files"))
        {
            Patterns = ["*.sql"],
            MimeTypes = ["text/plain"],
        };

        string suggestedName = string.IsNullOrWhiteSpace(_vm.ActiveTab.FallbackTitle)
            ? "script.sql"
            : $"{Path.GetFileNameWithoutExtension(_vm.ActiveTab.FallbackTitle)}.sql";

        IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = L("sqlEditor.saveSql.pickerTitle", "Save SQL File"),
                DefaultExtension = "sql",
                SuggestedFileName = suggestedName,
                FileTypeChoices = [sqlFileType],
            });

        string? path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        await _vm.SaveActiveTabAsync(path);
    }

    private async void ExportReportBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm is null)
            return;

        if (!_vm.TryBuildReportExportContext(out SqlEditorReportExportContext? context) || context is null)
        {
            _vm.PublishStatus(
                L("sqlEditor.export.status.noResultTitle", "No execution result available for export."),
                L("sqlEditor.export.status.noResultDetail", "Execute a query first."),
                hasError: true);
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window owner || topLevel.StorageProvider is null)
            return;

        var dialogVm = new SqlEditorReportExportDialogViewModel(context.TabTitle);
        var dialog = new SqlEditorReportExportDialogWindow(dialogVm);

        await dialog.ShowDialog(owner);
        if (!dialog.WasConfirmed)
            return;

        string normalizedExtension = dialogVm.SuggestedExtension.TrimStart('.');
        var reportFileType = GetExportFileType(dialogVm.SelectedType?.Type ?? SqlEditorReportType.HtmlFullFeature);

        IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = L("sqlEditor.export.pickerTitle", "Export SQL Data"),
                DefaultExtension = normalizedExtension,
                SuggestedFileName = dialogVm.FileName,
                FileTypeChoices = [reportFileType],
            });

        string? outputPath = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(outputPath))
            return;

        try
        {
            SqlEditorReportExportRequest request = dialogVm.BuildRequest(outputPath);
            string writtenPath = await _reportExportService.ExportAsync(context, request);
            _vm.PublishStatus(L("sqlEditor.export.status.successTitle", "Report exported."), writtenPath);
        }
        catch (Exception ex)
        {
            _vm.PublishStatus(L("sqlEditor.export.status.failedTitle", "Failed to export report."), ex.Message, hasError: true);
        }
    }

    private static FilePickerFileType GetExportFileType(SqlEditorReportType reportType)
    {
        return reportType switch
        {
            SqlEditorReportType.JsonContract => new FilePickerFileType(L("sqlEditor.export.fileType.json", "JSON File"))
            {
                Patterns = ["*.json"],
                MimeTypes = ["application/json", "text/plain"],
            },
            SqlEditorReportType.CsvData => new FilePickerFileType(L("sqlEditor.export.fileType.csv", "CSV File"))
            {
                Patterns = ["*.csv"],
                MimeTypes = ["text/csv", "text/plain"],
            },
            SqlEditorReportType.ExcelWorkbook => new FilePickerFileType(L("sqlEditor.export.fileType.xlsx", "Excel Workbook"))
            {
                Patterns = ["*.xlsx"],
                MimeTypes = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
            },
            _ => new FilePickerFileType(L("sqlEditor.export.fileType.html", "HTML File"))
            {
                Patterns = ["*.html", "*.htm"],
                MimeTypes = ["text/html", "text/plain"],
            },
        };
    }

    private void ShowCompletionWindow()
    {
        if (_vm is null || _editor is null)
            return;

        if (_completionWindow is not null)
            return;

        SqlCompletionRequest request = _vm.GetCompletionRequest(_editor.Text ?? string.Empty, _editor.CaretOffset);
        if (request.Suggestions.Count == 0)
            return;

        IReadOnlyList<SqlCompletionSuggestion> orderedSuggestions = request.Suggestions
            .OrderByDescending(static suggestion => suggestion.Kind == SqlCompletionKind.Keyword)
            .ThenBy(static suggestion => suggestion.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _completionWindow = new CompletionWindow(_editor.TextArea);
        ConfigureCompletionWindowVisuals(_completionWindow);
        foreach (SqlCompletionSuggestion suggestion in orderedSuggestions)
        {
            _completionWindow.CompletionList.CompletionData.Add(
                new SqlEditorCompletionData(
                    suggestion.Label,
                    suggestion.InsertText,
                    suggestion.Detail,
                    request.PrefixLength));
        }

        _completionWindow.Closed += (_, _) => _completionWindow = null;
        _completionWindow.Show();
    }

    private static void ConfigureCompletionWindowVisuals(CompletionWindow window)
    {
        IBrush popupBackground = ResolveBrush("Bg1Brush", UiColorConstants.C_08152A);
        IBrush popupBorder = ResolveBrush("BorderSubtleBrush", UiColorConstants.C_18355A);

        window.MinWidth = 320;
        window.MaxHeight = 360;

        ListBox? listBox = window.CompletionList.ListBox;
        if (listBox is null)
            return;

        listBox.Background = popupBackground;
        listBox.BorderBrush = popupBorder;
        listBox.BorderThickness = new Avalonia.Thickness(1);
        listBox.Margin = new Avalonia.Thickness(0);
    }

    private static IBrush ResolveBrush(string key, string fallbackHex)
    {
        if (Application.Current?.TryFindResource(key, out object? resource) == true && resource is IBrush brush)
            return brush;

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    private bool ShouldAutoTriggerCompletionAfterSpace()
    {
        if (_editor is null)
            return false;

        string text = _editor.Text ?? string.Empty;
        int caret = _editor.CaretOffset;
        if (caret <= 0 || caret > text.Length)
            return false;

        string beforeCaret = text[..caret].TrimEnd();
        return Regex.IsMatch(
            beforeCaret,
            @"\b(SELECT|FROM|JOIN|LEFT\s+JOIN|RIGHT\s+JOIN|INNER\s+JOIN|FULL\s+JOIN|WHERE|ON|GROUP\s+BY|ORDER\s+BY)\s*$",
            RegexOptions.IgnoreCase);
    }

    private static int? ResolveTabShortcutIndex(Key key, KeyModifiers modifiers)
    {
        if (!modifiers.HasFlag(KeyModifiers.Control))
            return null;

        return key switch
        {
            Key.D1 or Key.NumPad1 => 0,
            Key.D2 or Key.NumPad2 => 1,
            Key.D3 or Key.NumPad3 => 2,
            Key.D4 or Key.NumPad4 => 3,
            Key.D5 or Key.NumPad5 => 4,
            Key.D6 or Key.NumPad6 => 5,
            Key.D7 or Key.NumPad7 => 6,
            Key.D8 or Key.NumPad8 => 7,
            Key.D9 or Key.NumPad9 => 8,
            _ => null,
        };
    }

    private void FocusEditor()
    {
        if (_editor is null || !IsVisible)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (_editor is null || !_editor.IsVisible)
                return;

            EnsureEditorReady();
            _editor.Focus();
            _editor.TextArea?.Focus();
        }, DispatcherPriority.Background);
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
