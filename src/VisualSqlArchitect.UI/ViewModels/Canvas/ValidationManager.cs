using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.UI.ViewModels.Canvas;

/// <summary>
/// Manages query graph validation, including orphan node detection and validation issue tracking.
/// Runs validation asynchronously with debouncing to avoid excessive processing.
/// </summary>
public sealed class ValidationManager(CanvasViewModel canvasViewModel) : ViewModelBase
{
    private readonly CanvasViewModel _canvasViewModel = canvasViewModel;
    private CancellationTokenSource? _validationCts;

    private bool _hasErrors;
    private int _errorCount;
    private int _warningCount;
    private bool _hasOrphanNodes;
    private int _orphanCount;
    private bool _hasNamingViolations;
    private int _namingConformance;

    public bool HasErrors
    {
        get => _hasErrors;
        private set => Set(ref _hasErrors, value);
    }
    public int ErrorCount
    {
        get => _errorCount;
        private set => Set(ref _errorCount, value);
    }
    public int WarningCount
    {
        get => _warningCount;
        private set => Set(ref _warningCount, value);
    }
    public bool HasOrphanNodes
    {
        get => _hasOrphanNodes;
        private set => Set(ref _hasOrphanNodes, value);
    }
    public int OrphanCount
    {
        get => _orphanCount;
        private set => Set(ref _orphanCount, value);
    }
    public bool HasNamingViolations
    {
        get => _hasNamingViolations;
        private set => Set(ref _hasNamingViolations, value);
    }
    public int NamingConformance
    {
        get => _namingConformance;
        private set => Set(ref _namingConformance, value);
    }

    public RelayCommand? CleanupOrphansCommand { get; set; }
    public RelayCommand? AutoFixNamingCommand { get; set; }

    /// <summary>
    /// Schedules a validation run with 200ms debounce to avoid excessive processing.
    /// Cancels any pending validation before scheduling a new one.
    /// </summary>
    public void ScheduleValidation()
    {
        _validationCts?.Cancel();
        _validationCts = new CancellationTokenSource();
        CancellationToken token = _validationCts.Token;
        Task.Delay(200, token)
            .ContinueWith(
                _ =>
                {
                    if (!token.IsCancellationRequested)
                        Avalonia.Threading.Dispatcher.UIThread.Post(RunValidation);
                },
                TaskScheduler.Default
            );
    }

    /// <summary>
    /// Runs validation immediately: checks graph validity, orphan nodes, and naming conventions.
    /// Updates all validation properties and notifies command buttons of state changes.
    /// </summary>
    private void RunValidation()
    {
        IReadOnlyList<ValidationIssue> allIssues = GraphValidator.Validate(_canvasViewModel);
        var byNode = allIssues
            .Where(i => !string.IsNullOrEmpty(i.NodeId))
            .GroupBy(i => i.NodeId)
            .ToDictionary(g => g.Key, g => (IEnumerable<ValidationIssue>)g);

        // Detect orphan nodes and mark them for visual dimming
        IReadOnlySet<string> orphanIds = OrphanNodeDetector.DetectOrphanIds(_canvasViewModel);
        foreach (NodeViewModel node in _canvasViewModel.Nodes)
        {
            node.IsOrphan = orphanIds.Contains(node.Id);
            node.SetValidation(
                byNode.TryGetValue(node.Id, out IEnumerable<ValidationIssue>? issues) ? issues : []
            );
        }

        // Update validation summary in a single pass over all nodes
        bool hasErrors = false;
        bool hasOrphans = false;
        bool hasNaming = false;
        int errorCount = 0;
        int warningCount = 0;
        int orphanCount = 0;
        foreach (NodeViewModel node in _canvasViewModel.Nodes)
        {
            if (node.HasError) hasErrors = true;
            if (node.IsOrphan) { hasOrphans = true; orphanCount++; }
            foreach (ValidationIssue issue in node.ValidationIssues)
            {
                if (issue.Severity == IssueSeverity.Error) errorCount++;
                else if (issue.Severity == IssueSeverity.Warning) warningCount++;
                if (issue.Code.StartsWith("NAMING_")) hasNaming = true;
            }
        }
        HasErrors = hasErrors;
        ErrorCount = errorCount;
        WarningCount = warningCount;
        HasOrphanNodes = hasOrphans;
        OrphanCount = orphanCount;
        HasNamingViolations = hasNaming;
        NamingConformance = NamingConventionValidator.ConformancePercent(_canvasViewModel);

        // Notify command buttons of state changes
        CleanupOrphansCommand?.NotifyCanExecuteChanged();
        AutoFixNamingCommand?.NotifyCanExecuteChanged();
    }
}
