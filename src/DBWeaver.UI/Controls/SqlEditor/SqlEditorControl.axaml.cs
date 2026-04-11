using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Search;
using Avalonia.Input;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using DBWeaver.UI.Services;
using DBWeaver.UI.Services.SqlEditor;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Controls.SqlEditor;

[ExcludeFromCodeCoverage]
public partial class SqlEditorControl : UserControl
{
    private const double ResultsSheetMinHeight = 160;
    private const double ResultsSheetMaxHeightFallback = 720;
    private const int LargeEditorCompletionThreshold = 10_000;
    private const int CompletionDebounceMs = 80;

    private TextEditor? _editor;
    private Control? _resultsSheetHost;
    private Control? _resultsResizeGrip;
    private Border? _goToLineOverlay;
    private TextBlock? _goToLineLabel;
    private TextBox? _goToLineInput;
    private IBrush? _goToLineDefaultBorderBrush;
    private IBrush? _goToLineHighlightBrush;
    private IBrush? _defaultTextViewCurrentLineBackground;
    private SqlBracketHighlightRenderer? _bracketHighlightRenderer;
    private SqlExecutionStatementHighlightRenderer? _executionStatementHighlightRenderer;
    private FoldingManager? _foldingManager;
    private readonly SqlFoldingStrategy _sqlFoldingStrategy = new();
    private readonly SqlTokenizer _sqlTokenizer = new();
    private DispatcherTimer? _foldingRefreshTimer;
    private DispatcherTimer? _foldingChordTimer;
    private bool _awaitingFoldingChord;
    private DispatcherTimer? _goToLineHighlightTimer;
    private int _goToLineOriginCaretOffset;
    private SqlEditorViewModel? _vm;
    private SearchPanel? _searchPanel;
    private CompletionWindow? _completionWindow;
    private DispatcherTimer? _completionDebounceTimer;
    private bool _completionOnDemandHintShown;
    private long _completionRequestVersion;
    private bool _isResizingResultsSheet;
    private Point _resizeStartPointerPosition;
    private double _resizeStartHeight;

    public SqlEditorControl()
    {
        InitializeComponent();
        ConfigureTextEditor();
        DataContextChanged += (_, _) => AttachViewModel();
        AttachedToVisualTree += (_, _) => FocusEditor();
        Loaded += (_, _) => EnsureEditorReady();
        DetachedFromVisualTree += (_, _) => DisposeFoldingManager();
    }

    private void ConfigureTextEditor()
    {
        _editor = this.FindControl<TextEditor>("SqlTextEditor");
        _resultsSheetHost = this.FindControl<Control>("ResultsSheetHost");
        _resultsResizeGrip = this.FindControl<Control>("ResultsResizeGrip");
        _goToLineOverlay = this.FindControl<Border>("GoToLineOverlay");
        _goToLineLabel = this.FindControl<TextBlock>("GoToLineLabel");
        _goToLineInput = this.FindControl<TextBox>("GoToLineInput");
        _goToLineDefaultBorderBrush = _goToLineInput?.BorderBrush;
        ConfigureGoToLineLocalization();
        if (_editor is null)
            return;

        _editor.Options.EnableHyperlinks = false;
        _editor.Options.EnableEmailHyperlinks = false;
        _editor.Options.HighlightCurrentLine = true;
        _editor.Options.AllowScrollBelowDocument = true;
        _editor.Options.EnableRectangularSelection = true;
        _editor.Options.IndentationSize = 2;
        _editor.Options.ConvertTabsToSpaces = true;
        _editor.IsReadOnly = false;
        _editor.Focusable = true;
        _editor.IsHitTestVisible = true;
        _editor.Cursor = new Cursor(StandardCursorType.Ibeam);
        _editor.SyntaxHighlighting = SqlEditorHighlightingService.GetSqlDefinition();
        _searchPanel = SearchPanel.Install(_editor);
        ConfigureBracketHighlighting();
        _defaultTextViewCurrentLineBackground = _editor.TextArea?.TextView.CurrentLineBackground;
        EnsureEditorDocument();
        _editor.TextChanged += OnEditorTextChanged;
        if (_editor.TextArea is TextArea textArea)
        {
            textArea.TextEntered += OnEditorTextEntered;
            textArea.Caret.PositionChanged += OnEditorCaretPositionChanged;
            ConfigureSqlFolding(textArea);
        }
        _editor.PointerPressed += (_, _) => FocusEditor();
        ApplyEditorExecutionState();
        RefreshBracketHighlight();
        ScheduleFoldingRefresh();
    }

    private void AttachViewModel()
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;

        _vm = DataContext as SqlEditorViewModel;
        if (_vm is not null)
            _vm.PropertyChanged += OnViewModelPropertyChanged;

        SyncEditorTextFromViewModel();
        ApplyEditorExecutionState();
        FocusEditor();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SqlEditorViewModel.ActiveTab))
            SyncEditorTextFromViewModel();

        if (e.PropertyName == nameof(SqlEditorViewModel.IsExecuting))
            ApplyEditorExecutionState();

        if (e.PropertyName is nameof(SqlEditorViewModel.ActiveExecutionStatementStartLine)
            or nameof(SqlEditorViewModel.ActiveExecutionStatementEndLine)
            or nameof(SqlEditorViewModel.IsExecuting))
        {
            RefreshExecutionStatementHighlight();
        }
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
        ApplyEditorExecutionState();
        UpdateViewModelCursorPosition();
        RefreshExecutionStatementHighlight();
    }

    private void ApplyEditorExecutionState()
    {
        if (_editor is null)
            return;

        bool isExecuting = _vm?.IsExecuting == true;
        _editor.IsReadOnly = isExecuting;
        _editor.Cursor = new Cursor(isExecuting ? StandardCursorType.Wait : StandardCursorType.Ibeam);
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
        _vm.NotifyActiveTabEdited();
        RefreshBracketHighlight();
        ScheduleFoldingRefresh();
    }

    private void OnEditorTextEntered(object? sender, TextInputEventArgs e)
    {
        if (_editor is null || _vm is null)
            return;

        if (string.Equals(e.Text, ".", StringComparison.Ordinal))
        {
            TriggerCompletion(autoTriggered: true, allowDebounce: false);
            return;
        }

        if (string.Equals(e.Text, " ", StringComparison.Ordinal) && ShouldAutoTriggerCompletionAfterSpace())
        {
            TriggerCompletion(autoTriggered: true, allowDebounce: false);
            return;
        }

        if (ShouldDebounceCompletionAfterTextInput(e.Text))
            TriggerCompletion(autoTriggered: true, allowDebounce: true);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_vm is null || _editor is null || e.Handled)
            return;

        bool isExecuteSelection = e.Key == Key.F8;
        bool isExecuteAll = e.Key == Key.F5;
        bool isExplain = e.Key == Key.F4;
        bool isBenchmark = e.Key == Key.F6;
        bool isCompletion = e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool isFind = e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool isReplace = e.Key == Key.H && e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool isGoToLine = e.Key == Key.G && e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool isFormatSql = e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool isCollapseCurrent = e.Key == Key.OemOpenBrackets && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool isExpandCurrent = e.Key == Key.OemCloseBrackets && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool isStartFoldingChord = e.Key == Key.K && e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool isSave = e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool isOpen = e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool isNewTab = e.Key == Key.T && e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool isCloseTab = e.Key == Key.W && e.KeyModifiers.HasFlag(KeyModifiers.Control);
        int? tabShortcutIndex = ResolveTabShortcutIndex(e.Key, e.KeyModifiers);
        bool isExecuteCurrent = (e.Key is Key.Enter or Key.Return) && e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool isCancel = e.Key == Key.Escape;

        if (HandleFoldingChord(e))
            return;

        if (isCancel)
        {
            if (_searchPanel?.IsOpened == true)
            {
                _searchPanel.Close();
                e.Handled = true;
                return;
            }

            _vm.CancelExecution();
            e.Handled = true;
            return;
        }

        if (isFind)
        {
            OpenSearchPanel(isReplaceMode: false);
            e.Handled = true;
            return;
        }

        if (isReplace)
        {
            OpenSearchPanel(isReplaceMode: true);
            e.Handled = true;
            return;
        }

        if (isExecuteAll)
        {
            _ = ExecuteAllWithToastAsync();
            e.Handled = true;
            return;
        }

        if (isExplain)
        {
            _ = _vm.RunExplainAsync(includeAnalyze: false);
            e.Handled = true;
            return;
        }

        if (isBenchmark)
        {
            _ = _vm.RunBenchmarkAsync();
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
            TriggerCompletion(autoTriggered: false, allowDebounce: false);
            e.Handled = true;
            return;
        }

        if (isGoToLine)
        {
            ShowGoToLineOverlay();
            e.Handled = true;
            return;
        }

        if (isFormatSql)
        {
            _ = FormatSqlWithToastAsync();
            e.Handled = true;
            return;
        }

        if (isCollapseCurrent)
        {
            CollapseCurrentFolding();
            e.Handled = true;
            return;
        }

        if (isExpandCurrent)
        {
            ExpandCurrentFolding();
            e.Handled = true;
            return;
        }

        if (isStartFoldingChord)
        {
            StartFoldingChord();
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

    private void OpenSearchPanel(bool isReplaceMode)
    {
        if (_searchPanel is null)
            return;

        _searchPanel.IsReplaceMode = isReplaceMode;
        _searchPanel.Open();
    }

    private async void ExecuteOrCancelPrimaryButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm is null || _editor is null)
            return;

        if (_vm.IsExecuting)
        {
            if (_vm.CanExecuteOrCancel)
                _vm.CancelExecution();

            return;
        }

        SqlEditorResultSet result = await _vm.ExecuteSelectionOrCurrentAsync(
            _editor.SelectionStart,
            _editor.SelectionLength,
            _editor.CaretOffset);
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
            ShowShellToastSuccess(
                L("sqlEditor.toast.scriptSuccessTitle", "Script executado."),
                string.Format(
                    L("sqlEditor.toast.scriptSuccessDetail", "{0} instrucao(oes) executada(s) com sucesso."),
                    results.Count));
            return;
        }

        ShowShellToastWarning(
            L("sqlEditor.toast.scriptWarningTitle", "Script executado com falhas."),
            string.Format(
                L("sqlEditor.toast.scriptWarningDetail", "{0} de {1} instrucao(oes) falharam."),
                failures,
                results.Count));
    }

    private async Task FormatSqlWithToastAsync()
    {
        if (_editor?.Document is null)
            return;

        int selectionStart = _editor.SelectionStart;
        int selectionLength = _editor.SelectionLength;

        bool formatSelectionOnly = selectionLength > 0;
        string sourceSql = formatSelectionOnly
            ? _editor.Document.GetText(selectionStart, selectionLength)
            : _editor.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(sourceSql))
            return;

        var stopwatch = Stopwatch.StartNew();
        Task<string> formatTask = Task.Run(() => SqlDisplayFormatter.Format(sourceSql));
        bool formattingToastShown = false;

        Task completed = await Task.WhenAny(formatTask, Task.Delay(300));
        if (completed != formatTask)
        {
            ShowShellToastSuccess(L("sqlEditor.format.status.runningTitle", "Formatando SQL..."), null);
            formattingToastShown = true;
        }

        string formatted;
        try
        {
            formatted = await formatTask;
        }
        catch
        {
            ShowShellToastError(
                L("sqlEditor.format.status.failedTitle", "Nao foi possivel formatar SQL."),
                L("sqlEditor.format.status.failedDetail", "Verifique a sintaxe SQL e tente novamente."));
            return;
        }

        if (string.IsNullOrWhiteSpace(formatted))
        {
            ShowShellToastError(
                L("sqlEditor.format.status.failedTitle", "Nao foi possivel formatar SQL."),
                L("sqlEditor.format.status.failedDetail", "Verifique a sintaxe SQL e tente novamente."));
            return;
        }

        if (string.Equals(formatted, sourceSql, StringComparison.Ordinal))
            return;

        if (formatSelectionOnly)
        {
            _editor.Document.Replace(selectionStart, selectionLength, formatted);
            _editor.Select(selectionStart, formatted.Length);
        }
        else
        {
            int caret = _editor.CaretOffset;
            _editor.Document.Replace(0, _editor.Document.TextLength, formatted);
            _editor.CaretOffset = Math.Clamp(caret, 0, _editor.Document.TextLength);
            _editor.TextArea?.Caret.BringCaretToView();
        }

        stopwatch.Stop();
        if (formattingToastShown)
        {
            ShowShellToastSuccess(
                L("sqlEditor.format.status.successTitle", "SQL formatado."),
                string.Format(
                    L("sqlEditor.format.status.successDetail", "Concluido em {0} ms."),
                    (long)Math.Round(stopwatch.Elapsed.TotalMilliseconds)));
        }
    }

    private void ShowToastForResult(SqlEditorResultSet result)
    {
        if (!result.Success)
        {
            ShowShellToastError(L("sqlEditor.toast.resultErrorTitle", "Falha ao executar instrucao."), result.ErrorMessage);
            return;
        }

        long elapsedMs = (long)Math.Round(result.ExecutionTime.TotalMilliseconds);
        long rows = result.Data?.Rows.Count ?? result.RowsAffected ?? 0;
        string details = string.Format(
            L("sqlEditor.result.summary", "Linhas: {0}    Tempo: {1} ms"),
            rows,
            elapsedMs);
        ShowShellToastSuccess(L("sqlEditor.toast.resultSuccessTitle", "Execucao concluida com sucesso."), details);
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

        var sqlFileType = new FilePickerFileType(L("sqlEditor.saveSql.fileType", "Arquivos SQL"))
        {
            Patterns = ["*.sql", "*.txt"],
            MimeTypes = ["text/plain"],
        };

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = L("sqlEditor.openSql.pickerTitle", "Abrir arquivo SQL"),
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

        var sqlFileType = new FilePickerFileType(L("sqlEditor.saveSql.fileType", "Arquivos SQL"))
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
                Title = L("sqlEditor.saveSql.pickerTitle", "Salvar arquivo SQL"),
                DefaultExtension = "sql",
                SuggestedFileName = suggestedName,
                FileTypeChoices = [sqlFileType],
            });

        string? path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        await _vm.SaveActiveTabAsync(path);
    }

    private void TriggerCompletion(bool autoTriggered, bool allowDebounce)
    {
        if (_editor is null || _vm is null)
            return;

        if (autoTriggered && IsLargeEditorText())
        {
            PublishCompletionOnDemandHintOnce();
            return;
        }

        _completionOnDemandHintShown = false;

        if (!allowDebounce)
        {
            _completionDebounceTimer?.Stop();
            _ = ShowCompletionWindowAsync(allowUpdateExisting: true);
            return;
        }

        _completionDebounceTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(CompletionDebounceMs) };
        _completionDebounceTimer.Tick -= OnCompletionDebounceTick;
        _completionDebounceTimer.Tick += OnCompletionDebounceTick;
        _completionDebounceTimer.Stop();
        _completionDebounceTimer.Start();
    }

    private void OnCompletionDebounceTick(object? sender, EventArgs e)
    {
        _completionDebounceTimer?.Stop();
        _ = ShowCompletionWindowAsync(allowUpdateExisting: true);
    }

    private async Task ShowCompletionWindowAsync(bool allowUpdateExisting)
    {
        if (_vm is null || _editor is null)
            return;

        if (_completionWindow is not null && !allowUpdateExisting)
            return;

        long requestVersion = Interlocked.Increment(ref _completionRequestVersion);
        var stopwatch = Stopwatch.StartNew();
        string editorText = _editor.Text ?? string.Empty;
        int caretOffset = _editor.CaretOffset;

        Task<SqlCompletionRequest> computeTask = Task.Run(() => _vm.GetCompletionRequest(editorText, caretOffset));
        Task completed = await Task.WhenAny(computeTask, Task.Delay(CompletionDebounceMs));
        if (completed != computeTask)
            ShowCompletionLoadingPopup(allowUpdateExisting);

        SqlCompletionRequest request;
        try
        {
            request = await computeTask;
        }
        catch
        {
            return;
        }

        if (requestVersion != _completionRequestVersion)
            return;

        if (request.Suggestions.Count == 0)
        {
            _completionWindow?.Close();
            _completionWindow = null;
            stopwatch.Stop();
            _vm.RecordCompletionLatency(stopwatch.Elapsed);
            return;
        }

        IReadOnlyList<SqlCompletionSuggestion> orderedSuggestions = request.Suggestions
            .OrderByDescending(static suggestion => suggestion.Kind == SqlCompletionKind.Keyword)
            .ThenBy(static suggestion => suggestion.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (_completionWindow is null)
        {
            _completionWindow = new CompletionWindow(_editor.TextArea);
            _completionWindow.Closed += (_, _) => _completionWindow = null;
            _completionWindow.Show();
        }

        _completionWindow.CompletionList.CompletionData.Clear();
        foreach (SqlCompletionSuggestion suggestion in orderedSuggestions)
        {
            _completionWindow.CompletionList.CompletionData.Add(
                new SqlEditorCompletionData(
                    suggestion.Label,
                    suggestion.InsertText,
                    suggestion.Detail,
                    request.PrefixLength,
                    acceptedCallback: label => _vm.RecordCompletionSuggestionAccepted(label)));
        }

        stopwatch.Stop();
        _vm.RecordCompletionLatency(stopwatch.Elapsed);
    }

    private void ShowCompletionLoadingPopup(bool allowUpdateExisting)
    {
        if (_editor is null)
            return;

        if (_completionWindow is not null && !allowUpdateExisting)
            return;

        if (_completionWindow is null)
        {
            _completionWindow = new CompletionWindow(_editor.TextArea);
            _completionWindow.Closed += (_, _) => _completionWindow = null;
            _completionWindow.Show();
        }

        _completionWindow.CompletionList.CompletionData.Clear();
        _completionWindow.CompletionList.CompletionData.Add(
            new SqlEditorCompletionLoadingData(
                L("sqlEditor.completion.loading", "Carregando sugestoes...")));
    }

    private bool ShouldDebounceCompletionAfterTextInput(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || _editor is null)
            return false;

        char c = text[0];
        if (char.IsLetter(c) || c == '_')
            return true;

        if (!char.IsDigit(c))
            return false;

        string editorText = _editor.Text ?? string.Empty;
        int caret = _editor.CaretOffset;
        if (caret <= 1 || caret > editorText.Length)
            return false;

        char previous = editorText[caret - 2];
        return char.IsLetter(previous) || previous is '_' or '.';
    }

    private bool IsLargeEditorText()
    {
        return (_editor?.Text?.Length ?? 0) > LargeEditorCompletionThreshold;
    }

    private void PublishCompletionOnDemandHintOnce()
    {
        if (_vm is null || _completionOnDemandHintShown)
            return;

        _vm.PublishStatus(
            L("sqlEditor.status.completionOnDemand", "Editor grande — completion sob demanda (Ctrl+Space)."),
            null,
            hasError: false);
        _completionOnDemandHintShown = true;
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

    private void ResultsResizeGrip_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm is null || _resultsResizeGrip is null)
            return;

        PointerPointProperties properties = e.GetCurrentPoint(_resultsResizeGrip).Properties;
        if (!properties.IsLeftButtonPressed)
            return;

        _isResizingResultsSheet = true;
        _resizeStartPointerPosition = e.GetPosition(this);
        _resizeStartHeight = Math.Max(ResultsSheetMinHeight, _vm.ResultsSheetHeight);
        e.Pointer.Capture(_resultsResizeGrip);
        e.Handled = true;
    }

    private void ResultsResizeGrip_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isResizingResultsSheet || _vm is null)
            return;

        Point current = e.GetPosition(this);
        double deltaY = _resizeStartPointerPosition.Y - current.Y;
        double maxHeight = ResolveResultsSheetMaxHeight();
        double target = Math.Clamp(_resizeStartHeight + deltaY, ResultsSheetMinHeight, maxHeight);
        _vm.SetResultsSheetHeight(target);
        e.Handled = true;
    }

    private void ResultsResizeGrip_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        EndResultsResize(e.Pointer);
        e.Handled = true;
    }

    private void ResultsResizeGrip_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        EndResultsResize(null);
    }

    private void EndResultsResize(IPointer? pointer)
    {
        if (!_isResizingResultsSheet)
            return;

        _isResizingResultsSheet = false;
        pointer?.Capture(null);
        _vm?.PersistResultsSheetHeightPreference();
    }

    private double ResolveResultsSheetMaxHeight()
    {
        if (_resultsSheetHost?.Parent is not Control parent || parent.Bounds.Height <= 0)
            return ResultsSheetMaxHeightFallback;

        double available = parent.Bounds.Height - 80;
        if (available < ResultsSheetMinHeight)
            return ResultsSheetMinHeight;

        return available;
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

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private void ConfigureBracketHighlighting()
    {
        if (_editor?.TextArea?.TextView is not TextView textView)
            return;

        _bracketHighlightRenderer ??= new SqlBracketHighlightRenderer();
        if (!textView.BackgroundRenderers.Contains(_bracketHighlightRenderer))
            textView.BackgroundRenderers.Add(_bracketHighlightRenderer);

        _executionStatementHighlightRenderer ??= new SqlExecutionStatementHighlightRenderer();
        if (!textView.BackgroundRenderers.Contains(_executionStatementHighlightRenderer))
            textView.BackgroundRenderers.Add(_executionStatementHighlightRenderer);
    }

    private void OnEditorCaretPositionChanged(object? sender, EventArgs e)
    {
        RefreshBracketHighlight();
        UpdateViewModelCursorPosition();
    }

    private void RefreshExecutionStatementHighlight()
    {
        if (_editor?.TextArea?.TextView is not TextView textView)
            return;

        _executionStatementHighlightRenderer ??= new SqlExecutionStatementHighlightRenderer();
        int start = _vm?.IsExecuting == true ? _vm.ActiveExecutionStatementStartLine : 0;
        int end = _vm?.IsExecuting == true ? _vm.ActiveExecutionStatementEndLine : 0;
        _executionStatementHighlightRenderer.Update(start, end);
        textView.InvalidateLayer(KnownLayer.Selection);
    }

    private void UpdateViewModelCursorPosition()
    {
        if (_editor?.TextArea?.Caret is not Caret caret || _vm is null)
            return;

        _vm.UpdateCursorPosition(caret.Line, caret.Column);
    }

    private void RefreshBracketHighlight()
    {
        if (_editor is null || _bracketHighlightRenderer is null)
            return;

        _bracketHighlightRenderer.Update(_editor.Text ?? string.Empty, _editor.CaretOffset);
        _editor.TextArea?.TextView.InvalidateLayer(_bracketHighlightRenderer.Layer);
    }

    private void ConfigureSqlFolding(TextArea textArea)
    {
        _foldingManager = FoldingManager.Install(textArea);
    }

    private void DisposeFoldingManager()
    {
        if (_foldingManager is not null)
            FoldingManager.Uninstall(_foldingManager);

        _foldingManager = null;
    }

    private void ScheduleFoldingRefresh()
    {
        if (_editor?.Document is null || _foldingManager is null)
            return;

        _foldingRefreshTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _foldingRefreshTimer.Tick -= OnFoldingRefreshTimerTick;
        _foldingRefreshTimer.Tick += OnFoldingRefreshTimerTick;
        _foldingRefreshTimer.Stop();
        _foldingRefreshTimer.Start();
    }

    private void OnFoldingRefreshTimerTick(object? sender, EventArgs e)
    {
        _foldingRefreshTimer?.Stop();

        if (_editor?.Document is null || _foldingManager is null)
            return;

        IReadOnlyList<DBWeaver.UI.Services.SqlEditor.SqlToken> tokens = _sqlTokenizer.Tokenize(_editor.Text ?? string.Empty);
        _sqlFoldingStrategy.UpdateFoldings(_foldingManager, _editor.Document, tokens);
    }

    private void CollapseCurrentFolding()
    {
        if (_foldingManager is null || _editor is null)
            return;

        FoldingSection? current = _foldingManager
            .GetFoldingsContaining(_editor.CaretOffset)
            .OrderBy(f => f.Length)
            .FirstOrDefault();

        if (current is not null)
            current.IsFolded = true;
    }

    private void ExpandCurrentFolding()
    {
        if (_foldingManager is null || _editor is null)
            return;

        foreach (FoldingSection folding in _foldingManager.GetFoldingsContaining(_editor.CaretOffset))
            folding.IsFolded = false;
    }

    private bool HandleFoldingChord(KeyEventArgs e)
    {
        if (!_awaitingFoldingChord || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return false;

        if (e.Key == Key.D0)
        {
            CollapseAllFoldings();
            ResetFoldingChord();
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.J)
        {
            ExpandAllFoldings();
            ResetFoldingChord();
            e.Handled = true;
            return true;
        }

        ResetFoldingChord();
        return false;
    }

    private void StartFoldingChord()
    {
        _awaitingFoldingChord = true;
        _foldingChordTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _foldingChordTimer.Tick -= OnFoldingChordTimeout;
        _foldingChordTimer.Tick += OnFoldingChordTimeout;
        _foldingChordTimer.Stop();
        _foldingChordTimer.Start();
    }

    private void OnFoldingChordTimeout(object? sender, EventArgs e)
    {
        ResetFoldingChord();
    }

    private void ResetFoldingChord()
    {
        _awaitingFoldingChord = false;
        _foldingChordTimer?.Stop();
    }

    private void CollapseAllFoldings()
    {
        if (_foldingManager is null)
            return;

        foreach (FoldingSection folding in _foldingManager.AllFoldings)
            folding.IsFolded = true;
    }

    private void ExpandAllFoldings()
    {
        if (_foldingManager is null)
            return;

        foreach (FoldingSection folding in _foldingManager.AllFoldings)
            folding.IsFolded = false;
    }

    private void ConfigureGoToLineLocalization()
    {
        if (_goToLineLabel is not null)
            _goToLineLabel.Text = L("sqlEditor.gotoLine.label", "Ir para linha");

        if (_goToLineInput is not null)
            _goToLineInput.Watermark = L("sqlEditor.gotoLine.watermark", "Numero da linha");
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

    private void ShowGoToLineOverlay()
    {
        if (_editor is null || _goToLineOverlay is null || _goToLineInput is null)
            return;

        _goToLineOriginCaretOffset = _editor.CaretOffset;
        _goToLineInput.Text = (_editor.TextArea?.Caret.Line ?? 1).ToString(CultureInfo.InvariantCulture);
        ResetGoToLineValidationState();
        _goToLineOverlay.IsVisible = true;

        Dispatcher.UIThread.Post(() =>
        {
            _goToLineInput.Focus();
            _goToLineInput.SelectAll();
        }, DispatcherPriority.Background);
    }

    private void CursorPositionButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ShowGoToLineOverlay();
    }

    private void GoToLineInput_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseGoToLineOverlay(restoreOriginalCaret: true);
            e.Handled = true;
            return;
        }

        if (e.Key is not (Key.Enter or Key.Return))
            return;

        if (TryGoToLineFromInput())
        {
            CloseGoToLineOverlay(restoreOriginalCaret: false);
        }
        else
        {
            SetGoToLineValidationErrorState();
        }

        e.Handled = true;
    }

    private bool TryGoToLineFromInput()
    {
        if (_editor is null || _goToLineInput is null || _editor.Document is null)
            return false;

        if (!int.TryParse(_goToLineInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lineNumber))
            return false;

        if (lineNumber < 1 || lineNumber > _editor.Document.LineCount)
            return false;

        DocumentLine line = _editor.Document.GetLineByNumber(lineNumber);
        _editor.CaretOffset = line.Offset;
        _editor.ScrollToLine(lineNumber);
        _editor.TextArea?.Caret.BringCaretToView();
        HighlightGoToLine(lineNumber);
        return true;
    }

    private void CloseGoToLineOverlay(bool restoreOriginalCaret)
    {
        if (_goToLineOverlay is null)
            return;

        _goToLineOverlay.IsVisible = false;
        ResetGoToLineValidationState();

        if (restoreOriginalCaret && _editor is not null)
        {
            int maxOffset = _editor.Document?.TextLength ?? 0;
            _editor.CaretOffset = Math.Clamp(_goToLineOriginCaretOffset, 0, maxOffset);
            _editor.TextArea?.Caret.BringCaretToView();
        }

        FocusEditor();
    }

    private void ResetGoToLineValidationState()
    {
        if (_goToLineInput is null)
            return;

        _goToLineInput.BorderBrush = _goToLineDefaultBorderBrush;
    }

    private void SetGoToLineValidationErrorState()
    {
        if (_goToLineInput is null)
            return;

        if (Application.Current?.TryFindResource("StatusErrorBrush", out object? resource) == true && resource is IBrush brush)
        {
            _goToLineInput.BorderBrush = brush;
            return;
        }

        _goToLineInput.BorderBrush = _goToLineDefaultBorderBrush;
    }

    private void HighlightGoToLine(int lineNumber)
    {
        if (_editor?.TextArea?.TextView is not TextView textView)
            return;

        _defaultTextViewCurrentLineBackground ??= textView.CurrentLineBackground;
        _goToLineHighlightBrush ??= ResolveGoToLineHighlightBrush();

        textView.CurrentLineBackground = _goToLineHighlightBrush;
        textView.HighlightedLine = lineNumber;
        textView.InvalidateLayer(KnownLayer.Background);

        _goToLineHighlightTimer?.Stop();
        _goToLineHighlightTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _goToLineHighlightTimer.Tick -= OnGoToLineHighlightTimerTick;
        _goToLineHighlightTimer.Tick += OnGoToLineHighlightTimerTick;
        _goToLineHighlightTimer.Start();
    }

    private IBrush ResolveGoToLineHighlightBrush()
    {
        if (Application.Current?.TryFindResource("AccentSubtleBrush", out object? accentBrush) == true && accentBrush is IBrush resolvedAccent)
            return resolvedAccent;

        return _defaultTextViewCurrentLineBackground ?? Brushes.Transparent;
    }

    private void OnGoToLineHighlightTimerTick(object? sender, EventArgs e)
    {
        _goToLineHighlightTimer?.Stop();

        if (_editor?.TextArea?.TextView is not TextView textView)
            return;

        textView.HighlightedLine = 0;
        if (_defaultTextViewCurrentLineBackground is not null)
            textView.CurrentLineBackground = _defaultTextViewCurrentLineBackground;

        textView.InvalidateLayer(KnownLayer.Background);
    }
}
