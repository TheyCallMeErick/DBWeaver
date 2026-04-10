using Material.Icons;
using System.Collections.ObjectModel;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services.AppDiagnostics.Models;

public sealed class AppDiagnosticCategoryViewModel : ViewModelBase
{
    private bool _isExpanded = true;
    private readonly Lock _itemsGate = new();
    private string _title = string.Empty;

    public string Key { get; init; } = string.Empty;
    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }
    public MaterialIconKind IconKind { get; init; } = MaterialIconKind.HelpCircle;

    public ObservableCollection<AppDiagnosticEntry> Items { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public int ErrorCount
    {
        get
        {
            lock (_itemsGate)
                return Items.Count(i => i.Status == DiagnosticStatus.Error);
        }
    }

    public int WarningCount
    {
        get
        {
            lock (_itemsGate)
                return Items.Count(i => i.Status == DiagnosticStatus.Warning);
        }
    }

    public int TotalCount
    {
        get
        {
            lock (_itemsGate)
                return Items.Count;
        }
    }

    public bool HasIssues => ErrorCount > 0 || WarningCount > 0;
    public bool HasErrors => ErrorCount > 0;
    public bool HasWarnings => WarningCount > 0;

    public void ReplaceItems(IEnumerable<AppDiagnosticEntry> entries)
    {
        lock (_itemsGate)
        {
            Items.Clear();
            foreach (AppDiagnosticEntry entry in entries)
                Items.Add(entry);
        }

        RaisePropertyChanged(nameof(ErrorCount));
        RaisePropertyChanged(nameof(WarningCount));
        RaisePropertyChanged(nameof(TotalCount));
        RaisePropertyChanged(nameof(HasIssues));
        RaisePropertyChanged(nameof(HasErrors));
        RaisePropertyChanged(nameof(HasWarnings));
    }

    public IReadOnlyList<AppDiagnosticEntry> SnapshotItems()
    {
        lock (_itemsGate)
            return [.. Items];
    }
}
