using System.Collections.ObjectModel;
using System.Text;
using Material.Icons;
using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.UI.ViewModels;

// ── Diagnostic status ─────────────────────────────────────────────────────────

public enum DiagnosticStatus
{
    Ok,
    Warning,
    Error,
}

// ── Diagnostic entry ──────────────────────────────────────────────────────────

public sealed class AppDiagnosticEntry : ViewModelBase
{
    private DiagnosticStatus _status;
    private string _details = "";
    private DateTime _lastCheckAt = DateTime.MinValue;

    public string Name { get; init; } = "";
    public string Recommendation { get; init; } = "";

    public DiagnosticStatus Status
    {
        get => _status;
        set
        {
            Set(ref _status, value);
            RaisePropertyChanged(nameof(StatusIcon));
            RaisePropertyChanged(nameof(StatusColor));
        }
    }

    public string Details
    {
        get => _details;
        set => Set(ref _details, value);
    }

    public DateTime LastCheckAt
    {
        get => _lastCheckAt;
        set
        {
            Set(ref _lastCheckAt, value);
            RaisePropertyChanged(nameof(LastCheckLabel));
        }
    }

    public string StatusIcon =>
        Status switch
        {
            DiagnosticStatus.Ok => "✓",
            DiagnosticStatus.Warning => "⚠",
            DiagnosticStatus.Error => "✕",
            _ => "?",
        };

    public MaterialIconKind StatusIconKind =>
        Status switch
        {
            DiagnosticStatus.Ok => MaterialIconKind.CheckCircle,
            DiagnosticStatus.Warning => MaterialIconKind.Alert,
            DiagnosticStatus.Error => MaterialIconKind.CloseCircle,
            _ => MaterialIconKind.HelpCircle,
        };

    public string StatusColor =>
        Status switch
        {
            DiagnosticStatus.Ok => "#4ADE80",
            DiagnosticStatus.Warning => "#FBBF24",
            DiagnosticStatus.Error => "#EF4444",
            _ => "#4A5568",
        };

    public string LastCheckLabel =>
        _lastCheckAt == DateTime.MinValue ? "—" : _lastCheckAt.ToString("HH:mm:ss");
}

// ── ViewModel ─────────────────────────────────────────────────────────────────

public sealed class AppDiagnosticsViewModel : ViewModelBase
{
    private readonly CanvasViewModel _canvas;
    private bool _isVisible;
    private bool _isRunning;

    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }
    public bool IsRunning
    {
        get => _isRunning;
        set => Set(ref _isRunning, value);
    }

    public ObservableCollection<AppDiagnosticEntry> Entries { get; } = [];

    public RelayCommand RunChecksCommand { get; }
    public RelayCommand CloseCommand { get; }
    public RelayCommand CopyReportCommand { get; }

    // ── Summary ───────────────────────────────────────────────────────────────
    public int ErrorCount => Entries.Count(e => e.Status == DiagnosticStatus.Error);
    public int WarningCount => Entries.Count(e => e.Status == DiagnosticStatus.Warning);
    public DiagnosticStatus OverallStatus =>
        ErrorCount > 0 ? DiagnosticStatus.Error
        : WarningCount > 0 ? DiagnosticStatus.Warning
        : DiagnosticStatus.Ok;

    public string OverallLabel =>
        OverallStatus switch
        {
            DiagnosticStatus.Ok => "All systems OK",
            DiagnosticStatus.Warning => $"{WarningCount} warning(s) detected",
            DiagnosticStatus.Error => $"{ErrorCount} error(s) detected",
            _ => "",
        };

    public string OverallColor =>
        OverallStatus switch
        {
            DiagnosticStatus.Ok => "#4ADE80",
            DiagnosticStatus.Warning => "#FBBF24",
            DiagnosticStatus.Error => "#EF4444",
            _ => "#4A5568",
        };

    // ── Constructor ───────────────────────────────────────────────────────────

    public AppDiagnosticsViewModel(CanvasViewModel canvas)
    {
        _canvas = canvas;
        BuildEntries();

        RunChecksCommand = new RelayCommand(RunChecks, () => !IsRunning);
        CloseCommand = new RelayCommand(() => IsVisible = false);
        CopyReportCommand = new RelayCommand(CopyReport);
    }

    public void Open()
    {
        IsVisible = true;
        RunChecks();
    }

    /// <summary>
    /// Adds a transient informational entry (e.g. canvas migration notes).
    /// Does not open the panel automatically.
    /// </summary>
    public void AddInfo(string message)
    {
        Entries.Add(
            new AppDiagnosticEntry
            {
                Name = "Canvas Migration",
                Details = message,
                Recommendation = "Re-save the file to update it to the latest schema version.",
                Status = DiagnosticStatus.Warning,
                LastCheckAt = DateTime.Now,
            }
        );
        RaisePropertyChanged(nameof(ErrorCount));
        RaisePropertyChanged(nameof(WarningCount));
        RaisePropertyChanged(nameof(OverallStatus));
        RaisePropertyChanged(nameof(OverallLabel));
        RaisePropertyChanged(nameof(OverallColor));
    }

    // ── Check runners ─────────────────────────────────────────────────────────

    private void BuildEntries()
    {
        Entries.Clear();
        Entries.Add(
            new AppDiagnosticEntry
            {
                Name = "Canvas State",
                Recommendation = "Add at least one table node and one result output node",
            }
        );
        Entries.Add(
            new AppDiagnosticEntry
            {
                Name = "Validation Errors",
                Recommendation = "Fix highlighted nodes before running a preview",
            }
        );
        Entries.Add(
            new AppDiagnosticEntry
            {
                Name = "Orphan Nodes",
                Recommendation = "Use the '⊗ Orphan(s)' toolbar button to clean up unused nodes",
            }
        );
        Entries.Add(
            new AppDiagnosticEntry
            {
                Name = "Naming Conventions",
                Recommendation =
                    "Use the '⚑ Naming' toolbar button to auto-fix aliases to snake_case",
            }
        );
        Entries.Add(
            new AppDiagnosticEntry
            {
                Name = "SQL Generation",
                Recommendation = "Connect nodes to a Result Output node to generate SQL",
            }
        );
        Entries.Add(
            new AppDiagnosticEntry
            {
                Name = "Undo History",
                Recommendation = "History is in-memory only; save your canvas regularly",
            }
        );
    }

    private void RunChecks()
    {
        IsRunning = true;

        try
        {
            CheckCanvasState();
            CheckValidationErrors();
            CheckOrphanNodes();
            CheckNamingConventions();
            CheckSqlGeneration();
            CheckUndoHistory();
        }
        finally
        {
            IsRunning = false;
            NotifySummary();
        }
    }

    private void CheckCanvasState()
    {
        AppDiagnosticEntry entry = Entries[0];
        entry.LastCheckAt = DateTime.Now;

        if (_canvas.Nodes.Count == 0)
        {
            entry.Status = DiagnosticStatus.Warning;
            entry.Details = "Canvas is empty — no nodes present";
            return;
        }

        bool hasTable = _canvas.Nodes.Any(n => n.Type == NodeType.TableSource);
        bool hasOutput = _canvas.Nodes.Any(n => n.Type == NodeType.ResultOutput);

        if (!hasTable)
        {
            entry.Status = DiagnosticStatus.Warning;
            entry.Details = $"{_canvas.Nodes.Count} node(s) present but no table source";
        }
        else if (!hasOutput)
        {
            entry.Status = DiagnosticStatus.Warning;
            entry.Details = $"{_canvas.Nodes.Count} node(s) present but no Result Output";
        }
        else
        {
            entry.Status = DiagnosticStatus.Ok;
            entry.Details =
                $"{_canvas.Nodes.Count} node(s), {_canvas.Connections.Count} connection(s)";
        }
    }

    private void CheckValidationErrors()
    {
        AppDiagnosticEntry entry = Entries[1];
        entry.LastCheckAt = DateTime.Now;

        int errors = _canvas.ErrorCount;
        int warnings = _canvas.WarningCount;

        if (errors > 0)
        {
            entry.Status = DiagnosticStatus.Error;
            entry.Details = $"{errors} error(s) and {warnings} warning(s) in the graph";
        }
        else if (warnings > 0)
        {
            entry.Status = DiagnosticStatus.Warning;
            entry.Details = $"{warnings} warning(s) in the graph";
        }
        else
        {
            entry.Status = DiagnosticStatus.Ok;
            entry.Details = "No validation issues";
        }
    }

    private void CheckOrphanNodes()
    {
        AppDiagnosticEntry entry = Entries[2];
        entry.LastCheckAt = DateTime.Now;

        int count = _canvas.OrphanCount;
        if (count > 0)
        {
            entry.Status = DiagnosticStatus.Warning;
            entry.Details = $"{count} node(s) not connected to any output";
        }
        else
        {
            entry.Status = DiagnosticStatus.Ok;
            entry.Details = "No orphan nodes detected";
        }
    }

    private void CheckNamingConventions()
    {
        AppDiagnosticEntry entry = Entries[3];
        entry.LastCheckAt = DateTime.Now;

        int conformance = _canvas.NamingConformance;
        if (conformance < 100)
        {
            entry.Status = _canvas.HasNamingViolations
                ? DiagnosticStatus.Warning
                : DiagnosticStatus.Ok;
            entry.Details = $"Naming conformance: {conformance}%";
        }
        else
        {
            entry.Status = DiagnosticStatus.Ok;
            entry.Details = "All aliases follow naming conventions (100%)";
        }
    }

    private void CheckSqlGeneration()
    {
        AppDiagnosticEntry entry = Entries[4];
        entry.LastCheckAt = DateTime.Now;

        string? sql = _canvas.QueryText?.Trim();
        if (string.IsNullOrEmpty(sql))
        {
            entry.Status = DiagnosticStatus.Warning;
            entry.Details = "No SQL generated yet — connect nodes to a Result Output";
        }
        else if (sql.StartsWith("--") || sql.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
        {
            entry.Status = DiagnosticStatus.Error;
            entry.Details = "Last SQL generation produced an error";
        }
        else
        {
            entry.Status = DiagnosticStatus.Ok;
            string preview =
                sql.Length > 60 ? sql[..60].Replace('\n', ' ') + "…" : sql.Replace('\n', ' ');
            entry.Details = $"SQL OK · {preview}";
        }
    }

    private void CheckUndoHistory()
    {
        AppDiagnosticEntry entry = Entries[5];
        entry.LastCheckAt = DateTime.Now;

        int depth = _canvas.UndoRedo.UndoDepth;
        if (!_canvas.IsDirty)
        {
            entry.Status = DiagnosticStatus.Ok;
            entry.Details = "Canvas is saved (no unsaved changes)";
        }
        else if (depth > 50)
        {
            entry.Status = DiagnosticStatus.Warning;
            entry.Details = $"Unsaved changes with {depth} undo steps — consider saving";
        }
        else
        {
            entry.Status = DiagnosticStatus.Ok;
            entry.Details = $"Unsaved changes · {depth} undo step(s) available";
        }
    }

    // ── Report export ─────────────────────────────────────────────────────────

    private void CopyReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Visual SQL Architect — Diagnostic Report");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Overall: {OverallLabel}");
        sb.AppendLine(new string('─', 50));

        foreach (AppDiagnosticEntry e in Entries)
        {
            sb.AppendLine($"[{e.StatusIcon}] {e.Name}");
            sb.AppendLine($"    {e.Details}");
            if (e.Status != DiagnosticStatus.Ok)
                sb.AppendLine($"    → {e.Recommendation}");
        }

        if (
            Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is { } win
        )
        {
            Avalonia.Controls.TopLevel.GetTopLevel(win)?.Clipboard?.SetTextAsync(sb.ToString());
        }
    }

    private void NotifySummary()
    {
        RaisePropertyChanged(nameof(ErrorCount));
        RaisePropertyChanged(nameof(WarningCount));
        RaisePropertyChanged(nameof(OverallStatus));
        RaisePropertyChanged(nameof(OverallLabel));
        RaisePropertyChanged(nameof(OverallColor));
    }
}
