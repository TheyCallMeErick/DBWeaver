using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Material.Icons;
using DBWeaver.Nodes;
using DBWeaver.UI.Services.AppDiagnostics.Contracts;
using DBWeaver.UI.Services.AppDiagnostics.Models;
using DBWeaver.UI.Services.AppDiagnostics.Presentation;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.Services.QueryPreview.Models;
using DBWeaver.UI.Services.Validation;
using DBWeaver.UI.ViewModels.Canvas;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.ViewModels;

public sealed class AppDiagnosticsViewModel : ViewModelBase
{
    private readonly CanvasViewModel _canvas;
    private readonly ILocalizationService _localization;
    private readonly IAppDiagnosticsReportBuilder _reportBuilder;
    private readonly object _checksGate = new();
    private readonly ObservableCollection<AppDiagnosticEntry> _runtimeNotices = [];
    private bool _isVisible;
    private bool _isRunning;

    private readonly AppDiagnosticCategoryViewModel _canvasCategory;
    private readonly AppDiagnosticCategoryViewModel _outputCategory;
    private readonly AppDiagnosticCategoryViewModel _sessionCategory;
    private readonly AppDiagnosticCategoryViewModel _noticeCategory;
    private readonly PropertyChangedEventHandler _localizationChangedHandler;

    public AppDiagnosticsViewModel(
        CanvasViewModel canvas,
        ILocalizationService? localization = null,
        IAppDiagnosticsReportBuilder? reportBuilder = null)
    {
        _canvas = canvas;
        _localization = localization ?? LocalizationService.Instance;
        _reportBuilder = reportBuilder ?? new AppDiagnosticsReportBuilder(_localization);

        _canvasCategory = new AppDiagnosticCategoryViewModel
        {
            Key = "canvas",
            IconKind = MaterialIconKind.Alert,
        };
        _outputCategory = new AppDiagnosticCategoryViewModel
        {
            Key = "output",
            IconKind = MaterialIconKind.CodeBraces,
        };
        _sessionCategory = new AppDiagnosticCategoryViewModel
        {
            Key = "session",
            IconKind = MaterialIconKind.History,
        };
        _noticeCategory = new AppDiagnosticCategoryViewModel
        {
            Key = "notice",
            IconKind = MaterialIconKind.HelpCircle,
        };
        ApplyLocalizedCategoryTitles();
        _localizationChangedHandler = (_, e) =>
        {
            if (e.PropertyName is "" or "Item[]" or nameof(ILocalizationService.CurrentCulture))
            {
                ApplyLocalizedCategoryTitles();
                NotifySummary();
                RunChecks();
            }
        };
        _localization.PropertyChanged += _localizationChangedHandler;

        Categories = [_canvasCategory, _outputCategory, _sessionCategory, _noticeCategory];
        Entries = [];

        RunChecksCommand = new RelayCommand(RunChecks, () => !IsRunning);
        CloseCommand = new RelayCommand(() => IsVisible = false);
        CopyReportCommand = new RelayCommand(CopyReport);

        WireSignals();
        RunChecks();
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (!Set(ref _isRunning, value))
                return;

            RunChecksCommand.NotifyCanExecuteChanged();
        }
    }

    public ObservableCollection<AppDiagnosticCategoryViewModel> Categories { get; }
    public ObservableCollection<AppDiagnosticEntry> Entries { get; }

    public RelayCommand RunChecksCommand { get; }
    public RelayCommand CloseCommand { get; }
    public RelayCommand CopyReportCommand { get; }

    public int ErrorCount
    {
        get
        {
            lock (_checksGate)
                return Entries.Count(e => e.Status == DiagnosticStatus.Error);
        }
    }

    public int WarningCount
    {
        get
        {
            lock (_checksGate)
                return Entries.Count(e => e.Status == DiagnosticStatus.Warning);
        }
    }
    public int AttentionCount => ErrorCount + WarningCount;
    public bool HasAttention => AttentionCount > 0;
    public string AttentionCountLabel => AttentionCount.ToString();

    public DiagnosticStatus OverallStatus =>
        ErrorCount > 0
            ? DiagnosticStatus.Error
            : WarningCount > 0
                ? DiagnosticStatus.Warning
                : DiagnosticStatus.Ok;

    public string OverallLabel => OverallStatus switch
    {
        DiagnosticStatus.Ok => L("diagnostics.summary.ok", "All systems OK"),
        DiagnosticStatus.Warning => string.Format(
            L("diagnostics.summary.warningCount", "{0} warning(s) detected"),
            WarningCount),
        DiagnosticStatus.Error => string.Format(
            L("diagnostics.summary.errorCount", "{0} error(s) detected"),
            ErrorCount),
        _ => string.Empty,
    };

    public string OverallColor => OverallStatus switch
    {
        DiagnosticStatus.Ok => UiColorConstants.C_4ADE80,
        DiagnosticStatus.Warning => UiColorConstants.C_FBBF24,
        DiagnosticStatus.Error => UiColorConstants.C_EF4444,
        _ => UiColorConstants.C_4A5568,
    };

    public void Open()
    {
        IsVisible = true;
        RunChecks();
    }

    public void AddInfo(string message)
    {
        AddWarning(
            area: L("diagnostics.canvasMigration", "Canvas Migration"),
            message: message,
            recommendation: L("diagnostics.recommendation.resaveFile", "Re-save the file to update it to the latest schema version."),
            openPanel: false
        );
    }

    public void AddWarning(string area, string message, string recommendation, bool openPanel = false)
    {
        _runtimeNotices.Add(
            new AppDiagnosticEntry
            {
                Name = string.IsNullOrWhiteSpace(area) ? L("diagnostics.warning", "Warning") : area,
                Details = message,
                Recommendation = recommendation,
                Status = DiagnosticStatus.Warning,
                LastCheckAt = DateTime.Now,
            }
        );

        RunChecks();
        if (openPanel)
            Open();
    }

    private void WireSignals()
    {
        _canvas.PropertyChanged += OnAnySignalChanged;
        _canvas.Nodes.CollectionChanged += OnCollectionSignalChanged;
        _canvas.Connections.CollectionChanged += OnCollectionSignalChanged;
        if (_canvas.LiveSql is not null)
        {
            _canvas.LiveSql.PropertyChanged += OnAnySignalChanged;
            _canvas.LiveSql.ErrorHints.CollectionChanged += OnCollectionSignalChanged;
            _canvas.LiveSql.GuardIssues.CollectionChanged += OnCollectionSignalChanged;
            _canvas.LiveSql.DiagnosticItems.CollectionChanged += OnCollectionSignalChanged;
        }

        _canvas.DataPreview.PropertyChanged += OnAnySignalChanged;

        LiveDdlBarViewModel? liveDdl = _canvas.LiveDdl;
        if (liveDdl is null)
            return;

        liveDdl.PropertyChanged += OnAnySignalChanged;
        liveDdl.ErrorHints.CollectionChanged += OnCollectionSignalChanged;
        liveDdl.DiagnosticsPanel.Items.CollectionChanged += OnCollectionSignalChanged;
    }

    private void OnAnySignalChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RunChecks();
    }

    private void OnCollectionSignalChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RunChecks();
    }

    private void RunChecks()
    {
        lock (_checksGate)
        {
            if (IsRunning)
                return;

            IsRunning = true;
            try
            {
                List<AppDiagnosticEntry> canvasEntries = BuildCanvasEntries();
                List<AppDiagnosticEntry> outputEntries = BuildOutputEntries();
                List<AppDiagnosticEntry> sessionEntries = BuildSessionEntries();
                List<AppDiagnosticEntry> noticeEntries = [.. _runtimeNotices];

                _canvasCategory.ReplaceItems(canvasEntries);
                _outputCategory.ReplaceItems(outputEntries);
                _sessionCategory.ReplaceItems(sessionEntries);
                _noticeCategory.ReplaceItems(noticeEntries);

                RebuildFlattenedEntries();
            }
            finally
            {
                IsRunning = false;
                NotifySummary();
            }
        }
    }

    private List<AppDiagnosticEntry> BuildCanvasEntries()
    {
        bool isDdl = _canvas.LiveDdl is not null;
        string sourceNodeLabel = isDdl ? "DDL root node" : "table source";
        string outputNodeLabel = isDdl ? "DDL node graph" : "Result Output";

        var result = new List<AppDiagnosticEntry>();

        var canvasState = new AppDiagnosticEntry
        {
            Name = L("diagnostics.canvasState.name", "Canvas State"),
            Recommendation = string.Format(
                L("diagnostics.canvasState.recommendation", "Add at least one {0} and one {1}"),
                sourceNodeLabel,
                outputNodeLabel),
            LastCheckAt = DateTime.Now,
        };

        if (_canvas.Nodes.Count == 0)
        {
            canvasState.Status = DiagnosticStatus.Warning;
            canvasState.Details = L("diagnostics.canvasState.empty", "Canvas is empty - no nodes present");
        }
        else
        {
            canvasState.Status = DiagnosticStatus.Ok;
            canvasState.Details = string.Format(
                L("diagnostics.canvasState.counts", "{0} node(s), {1} connection(s)"),
                _canvas.Nodes.Count,
                _canvas.Connections.Count);
        }
        result.Add(canvasState);

        var validation = new AppDiagnosticEntry
        {
            Name = L("diagnostics.validation.name", "Validation Errors"),
            Recommendation = L("diagnostics.validation.recommendation", "Fix highlighted nodes before running output preview"),
            LastCheckAt = DateTime.Now,
        };
        if (_canvas.ErrorCount > 0)
        {
            validation.Status = DiagnosticStatus.Error;
            validation.Details = string.Format(
                L("diagnostics.validation.errorWithWarnings", "{0} error(s) and {1} warning(s) in the graph"),
                _canvas.ErrorCount,
                _canvas.WarningCount);
        }
        else if (_canvas.WarningCount > 0)
        {
            validation.Status = DiagnosticStatus.Warning;
            validation.Details = string.Format(
                L("diagnostics.validation.warningOnly", "{0} warning(s) in the graph"),
                _canvas.WarningCount);
        }
        else
        {
            validation.Status = DiagnosticStatus.Ok;
            validation.Details = L("diagnostics.validation.none", "No validation issues");
        }
        result.Add(validation);

        var orphan = new AppDiagnosticEntry
        {
            Name = L("diagnostics.orphan.name", "Orphan Nodes"),
            Recommendation = L("diagnostics.orphan.recommendation", "Use the orphan cleanup action to remove unused nodes"),
            LastCheckAt = DateTime.Now,
        };
        if (_canvas.OrphanCount > 0)
        {
            orphan.Status = DiagnosticStatus.Warning;
            orphan.Details = string.Format(
                L("diagnostics.orphan.count", "{0} node(s) not connected to any output"),
                _canvas.OrphanCount);
        }
        else
        {
            orphan.Status = DiagnosticStatus.Ok;
            orphan.Details = L("diagnostics.orphan.none", "No orphan nodes detected");
        }
        result.Add(orphan);

        var naming = new AppDiagnosticEntry
        {
            Name = L("diagnostics.naming.name", "Naming Conventions"),
            Recommendation = L("diagnostics.naming.recommendation", "Use auto-fix alias naming when conformance is below 100%"),
            LastCheckAt = DateTime.Now,
        };
        if (_canvas.NamingConformance < 100)
        {
            naming.Status = _canvas.HasNamingViolations ? DiagnosticStatus.Warning : DiagnosticStatus.Ok;
            naming.Details = string.Format(
                L("diagnostics.naming.conformance", "Naming conformance: {0}%"),
                _canvas.NamingConformance);
        }
        else
        {
            naming.Status = DiagnosticStatus.Ok;
            naming.Details = L("diagnostics.naming.ok", "All aliases follow naming conventions (100%)");
        }
        result.Add(naming);
        result.AddRange(BuildNodeValidationDetails());
        result.AddRange(BuildOrphanNodeDetails());

        return result;
    }

    private List<AppDiagnosticEntry> BuildOutputEntries()
    {
        return _canvas.LiveDdl is not null
            ? BuildDdlOutputEntries(_canvas.LiveDdl)
            : BuildQueryOutputEntries();
    }

    private List<AppDiagnosticEntry> BuildQueryOutputEntries()
    {
        var result = new List<AppDiagnosticEntry>();

        var compile = new AppDiagnosticEntry
        {
            Name = L("diagnostics.queryCompilation.name", "Live SQL Compilation"),
            Recommendation = L("diagnostics.queryCompilation.recommendation", "Review SQL diagnostics in output when errors/warnings are reported."),
            LastCheckAt = DateTime.Now,
        };
        if (!_canvas.LiveSql.IsValid || _canvas.LiveSql.ErrorHints.Count > 0)
        {
            compile.Status = DiagnosticStatus.Error;
            compile.Details = _canvas.LiveSql.ErrorHints.FirstOrDefault() ?? L("diagnostics.queryCompilation.errorFallback", "Live SQL compilation reported errors.");
        }
        else if (_canvas.LiveSql.DiagnosticItems.Count > 0 || _canvas.LiveSql.GuardIssues.Count > 0)
        {
            compile.Status = DiagnosticStatus.Warning;
            compile.Details = string.Format(
                L("diagnostics.queryCompilation.warningCounts", "{0} diagnostic item(s), {1} guardrail warning(s)."),
                _canvas.LiveSql.DiagnosticItems.Count,
                _canvas.LiveSql.GuardIssues.Count);
        }
        else
        {
            compile.Status = DiagnosticStatus.Ok;
            compile.Details = L("diagnostics.queryCompilation.ok", "Live SQL compiled without diagnostics.");
        }
        result.Add(compile);

        var previewSafety = new AppDiagnosticEntry
        {
            Name = L("diagnostics.previewSafety.name", "Preview Safety"),
            Recommendation = L("diagnostics.previewSafety.recommendation", "Preview executes read-only statements only."),
            LastCheckAt = DateTime.Now,
        };
        if (_canvas.LiveSql.IsMutatingCommand)
        {
            previewSafety.Status = DiagnosticStatus.Warning;
            previewSafety.Details = L("diagnostics.previewSafety.blocked", "Current SQL is mutating and blocked by Safe Preview mode.");
        }
        else
        {
            previewSafety.Status = DiagnosticStatus.Ok;
            previewSafety.Details = L("diagnostics.previewSafety.ok", "Preview safety checks passed.");
        }
        result.Add(previewSafety);

        var execution = new AppDiagnosticEntry
        {
            Name = L("diagnostics.previewExecution.name", "Preview Execution"),
            Recommendation = L("diagnostics.previewExecution.recommendation", "Run preview and inspect diagnostics for execution/runtime errors."),
            LastCheckAt = DateTime.Now,
        };
        switch (_canvas.DataPreview.CurrentState)
        {
            case PreviewExecutionState.Failed:
                execution.Status = DiagnosticStatus.Error;
                execution.Details = _canvas.DataPreview.Diagnostic?.FriendlyMessage
                    ?? _canvas.DataPreview.ErrorMessage
                    ?? L("diagnostics.previewExecution.failed", "Preview execution failed.");
                break;
            case PreviewExecutionState.Cancelled:
                execution.Status = DiagnosticStatus.Warning;
                execution.Details = L("diagnostics.previewExecution.cancelled", "Preview execution was cancelled.");
                break;
            case PreviewExecutionState.Done:
                execution.Status = DiagnosticStatus.Ok;
                execution.Details = string.Format(
                    L("diagnostics.previewExecution.done", "{0} row(s) in {1}ms."),
                    _canvas.DataPreview.RowCount,
                    _canvas.DataPreview.ExecutionMs);
                break;
            default:
                execution.Status = DiagnosticStatus.Ok;
                execution.Details = L("diagnostics.previewExecution.none", "No preview execution issues detected.");
                break;
        }
        result.Add(execution);
        result.AddRange(BuildLiveSqlDiagnosticDetails());
        result.AddRange(BuildLiveSqlGuardIssueDetails());

        return result;
    }

    private List<AppDiagnosticEntry> BuildDdlOutputEntries(LiveDdlBarViewModel liveDdl)
    {
        var result = new List<AppDiagnosticEntry>();

        var compile = new AppDiagnosticEntry
        {
            Name = L("diagnostics.ddlCompilation.name", "DDL Compilation"),
            Recommendation = L("diagnostics.ddlCompilation.recommendation", "Fix DDL compile diagnostics before execution."),
            LastCheckAt = DateTime.Now,
        };
        if (!liveDdl.IsValid)
        {
            compile.Status = DiagnosticStatus.Error;
            compile.Details = liveDdl.CompileError ?? liveDdl.ErrorHints.FirstOrDefault() ?? L("diagnostics.ddlCompilation.failed", "DDL compilation failed.");
        }
        else if (liveDdl.DiagnosticsPanel.HasItems && liveDdl.DiagnosticsPanel.WarningCount > 0)
        {
            compile.Status = DiagnosticStatus.Warning;
            compile.Details = string.Format(
                L("diagnostics.ddlCompilation.warningCount", "{0} warning(s) reported by DDL compiler."),
                liveDdl.DiagnosticsPanel.WarningCount);
        }
        else
        {
            compile.Status = DiagnosticStatus.Ok;
            compile.Details = L("diagnostics.ddlCompilation.ok", "DDL compilation succeeded.");
        }
        result.Add(compile);

        var output = new AppDiagnosticEntry
        {
            Name = L("diagnostics.ddlOutput.name", "DDL Output"),
            Recommendation = L("diagnostics.ddlOutput.recommendation", "Add/complete DDL nodes until at least one statement is generated."),
            LastCheckAt = DateTime.Now,
        };
        if (string.IsNullOrWhiteSpace(liveDdl.RawSql))
        {
            output.Status = DiagnosticStatus.Warning;
            output.Details = L("diagnostics.ddlOutput.none", "No DDL statements generated yet.");
        }
        else
        {
            output.Status = DiagnosticStatus.Ok;
            output.Details = string.Format(
                L("diagnostics.ddlOutput.lines", "{0} line(s) of DDL generated."),
                liveDdl.RawSql.Split('\n').Length);
        }
        result.Add(output);
        result.AddRange(BuildDdlDiagnosticDetails(liveDdl));

        return result;
    }

    private List<AppDiagnosticEntry> BuildSessionEntries()
    {
        var undo = new AppDiagnosticEntry
        {
            Name = L("diagnostics.undo.name", "Undo History"),
            Recommendation = L("diagnostics.undo.recommendation", "History is in-memory only; save your canvas regularly."),
            LastCheckAt = DateTime.Now,
        };

        int depth = _canvas.UndoRedo.UndoDepth;
        if (!_canvas.IsDirty)
        {
            undo.Status = DiagnosticStatus.Ok;
            undo.Details = L("diagnostics.undo.saved", "Canvas is saved (no unsaved changes).");
        }
        else if (depth > 50)
        {
            undo.Status = DiagnosticStatus.Warning;
            undo.Details = string.Format(
                L("diagnostics.undo.unsavedDeep", "Unsaved changes with {0} undo steps."),
                depth);
        }
        else
        {
            undo.Status = DiagnosticStatus.Ok;
            undo.Details = string.Format(
                L("diagnostics.undo.unsaved", "Unsaved changes - {0} undo step(s) available."),
                depth);
        }

        return [undo];
    }

    private IEnumerable<AppDiagnosticEntry> BuildNodeValidationDetails()
    {
        List<NodeViewModel> nodesSnapshot = SnapshotCollection(_canvas.Nodes);
        foreach (NodeViewModel node in nodesSnapshot)
        {
            List<ValidationIssue> issuesSnapshot = SnapshotCollection(node.ValidationIssues);
            foreach (ValidationIssue issue in issuesSnapshot)
            {
                yield return new AppDiagnosticEntry
                {
                    Name = L("diagnostics.validation.detailName", "Node validation"),
                    Code = issue.Code,
                    NodeId = issue.NodeId,
                    Details = issue.Message,
                    Location = BuildNodeLocation(issue.NodeId, null),
                    Recommendation = issue.Suggestion ?? L("diagnostics.validation.recommendation", "Fix highlighted nodes before running output preview"),
                    Status = issue.Severity == IssueSeverity.Error ? DiagnosticStatus.Error : DiagnosticStatus.Warning,
                    FocusCommand = BuildFocusNodeCommand(issue.NodeId),
                    LastCheckAt = DateTime.Now,
                };
            }
        }
    }

    private IEnumerable<AppDiagnosticEntry> BuildOrphanNodeDetails()
    {
        List<NodeViewModel> orphanSnapshot = SnapshotCollection(_canvas.Nodes)
            .Where(n => n.IsOrphan)
            .ToList();
        foreach (NodeViewModel node in orphanSnapshot)
        {
            yield return new AppDiagnosticEntry
            {
                Name = L("diagnostics.orphan.detailName", "Orphan node"),
                Code = "W-GRAPH-ORPHAN",
                NodeId = node.Id,
                Details = string.Format(
                    L("diagnostics.orphan.detailMessage", "Node '{0}' is disconnected from output flow."),
                    node.Title),
                Location = BuildNodeLocation(node.Id, null),
                Recommendation = L("diagnostics.orphan.recommendation", "Use the orphan cleanup action to remove unused nodes"),
                Status = DiagnosticStatus.Warning,
                FocusCommand = BuildFocusNodeCommand(node.Id),
                LastCheckAt = DateTime.Now,
            };
        }
    }

    private IEnumerable<AppDiagnosticEntry> BuildLiveSqlDiagnosticDetails()
    {
        List<PreviewDiagnostic> diagnosticsSnapshot = SnapshotCollection(_canvas.LiveSql.Diagnostics);
        foreach (PreviewDiagnostic diagnostic in diagnosticsSnapshot)
        {
            yield return new AppDiagnosticEntry
            {
                Name = L("diagnostics.queryCompilation.detailName", "SQL diagnostic"),
                Code = diagnostic.Code,
                NodeId = diagnostic.NodeId,
                PinName = diagnostic.PinName,
                Details = diagnostic.Message,
                Location = BuildNodeLocation(diagnostic.NodeId, diagnostic.PinName),
                Recommendation = L("diagnostics.queryCompilation.recommendation", "Review SQL diagnostics in output when errors/warnings are reported."),
                Status = MapPreviewSeverity(diagnostic.Severity),
                FocusCommand = BuildFocusNodeCommand(diagnostic.NodeId),
                LastCheckAt = DateTime.Now,
            };
        }
    }

    private IEnumerable<AppDiagnosticEntry> BuildLiveSqlGuardIssueDetails()
    {
        List<GuardIssue> guardIssuesSnapshot = SnapshotCollection(_canvas.LiveSql.GuardIssues);
        foreach (GuardIssue issue in guardIssuesSnapshot)
        {
            yield return new AppDiagnosticEntry
            {
                Name = L("diagnostics.guard.detailName", "Guardrail issue"),
                Code = issue.Code,
                Details = issue.Message,
                Recommendation = issue.Suggestion,
                Status = issue.Severity == GuardSeverity.Block ? DiagnosticStatus.Error : DiagnosticStatus.Warning,
                LastCheckAt = DateTime.Now,
            };
        }
    }

    private static List<T> SnapshotCollection<T>(IEnumerable<T> source)
    {
        const int maxAttempts = 3;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                var snapshot = new List<T>();
                foreach (T item in source)
                    snapshot.Add(item);
                return snapshot;
            }
            catch (Exception ex) when ((ex is InvalidOperationException || ex is ArgumentException) && attempt < maxAttempts - 1)
            {
                continue;
            }
        }

        return [];
    }

    private IEnumerable<AppDiagnosticEntry> BuildDdlDiagnosticDetails(LiveDdlBarViewModel liveDdl)
    {
        foreach (DdlDiagnosticItemViewModel item in liveDdl.DiagnosticsPanel.Items)
        {
            yield return new AppDiagnosticEntry
            {
                Name = L("diagnostics.ddlCompilation.detailName", "DDL diagnostic"),
                Code = item.Code,
                NodeId = item.NodeId,
                Details = item.Message,
                Location = BuildNodeLocation(item.NodeId, null),
                Recommendation = L("diagnostics.ddlCompilation.recommendation", "Fix DDL compile diagnostics before execution."),
                Status = item.IsError ? DiagnosticStatus.Error : DiagnosticStatus.Warning,
                FocusCommand = BuildFocusNodeCommand(item.NodeId),
                LastCheckAt = DateTime.Now,
            };
        }
    }

    private RelayCommand? BuildFocusNodeCommand(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return null;

        return new RelayCommand(
            () => _canvas.FocusNodeById(nodeId),
            () => _canvas.Nodes.Any(n => n.Id.Equals(nodeId, StringComparison.Ordinal))
        );
    }

    private string? BuildNodeLocation(string? nodeId, string? pinName)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return string.IsNullOrWhiteSpace(pinName)
                ? null
                : string.Format(
                    L("diagnostics.location.pinOnly", "Pin: {0}"),
                    pinName);

        NodeViewModel? node = _canvas.Nodes.FirstOrDefault(n => n.Id.Equals(nodeId, StringComparison.Ordinal));
        string nodeLabel = node is null
            ? nodeId
            : $"{node.Title} ({node.Id})";

        return string.IsNullOrWhiteSpace(pinName)
            ? string.Format(
                L("diagnostics.location.node", "Node: {0}"),
                nodeLabel)
            : string.Format(
                L("diagnostics.location.nodePin", "Node: {0} - Pin: {1}"),
                nodeLabel,
                pinName);
    }

    private static DiagnosticStatus MapPreviewSeverity(PreviewDiagnosticSeverity severity)
    {
        return severity switch
        {
            PreviewDiagnosticSeverity.Error => DiagnosticStatus.Error,
            PreviewDiagnosticSeverity.Warning => DiagnosticStatus.Warning,
            _ => DiagnosticStatus.Warning,
        };
    }

    private void RebuildFlattenedEntries()
    {
        Entries.Clear();
        foreach (AppDiagnosticCategoryViewModel category in Categories)
        {
            foreach (AppDiagnosticEntry entry in category.SnapshotItems())
                Entries.Add(entry);
        }
    }

    private void CopyReport()
    {
        string report = _reportBuilder.BuildReport(OverallLabel, Categories);
        _canvas.DataPreview.ShowError(report, null);
    }

    public IReadOnlyList<AppDiagnosticEntry> SnapshotEntries()
    {
        lock (_checksGate)
            return [.. Entries];
    }

    private void NotifySummary()
    {
        RaisePropertyChanged(nameof(ErrorCount));
        RaisePropertyChanged(nameof(WarningCount));
        RaisePropertyChanged(nameof(AttentionCount));
        RaisePropertyChanged(nameof(HasAttention));
        RaisePropertyChanged(nameof(AttentionCountLabel));
        RaisePropertyChanged(nameof(OverallStatus));
        RaisePropertyChanged(nameof(OverallLabel));
        RaisePropertyChanged(nameof(OverallColor));
    }

    private void ApplyLocalizedCategoryTitles()
    {
        _canvasCategory.Title = L("diagnostics.category.canvas", "Canvas Integrity");
        _outputCategory.Title = L("diagnostics.category.output", "Output & Execution");
        _sessionCategory.Title = L("diagnostics.category.session", "Session & Safety");
        _noticeCategory.Title = L("diagnostics.category.notice", "Runtime Notices");
    }

    private string L(string key, string fallback)
    {
        string value = _localization[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
