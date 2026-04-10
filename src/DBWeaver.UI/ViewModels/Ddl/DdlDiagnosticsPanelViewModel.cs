using System.Collections.ObjectModel;
using DBWeaver.Ddl;

namespace DBWeaver.UI.ViewModels;

public sealed class DdlDiagnosticsPanelViewModel : ViewModelBase
{
    private readonly Action<string>? _focusNodeById;

    public ObservableCollection<DdlDiagnosticItemViewModel> Items { get; } = [];

    public int ErrorCount => Items.Count(i => i.IsError);

    public int WarningCount => Items.Count(i => i.IsWarning);

    public bool HasItems => Items.Count > 0;

    public DdlDiagnosticsPanelViewModel(Action<string>? focusNodeById = null)
    {
        _focusNodeById = focusNodeById;
    }

    public void ReplaceDiagnostics(IReadOnlyList<DdlCompileDiagnostic> diagnostics)
    {
        Items.Clear();

        foreach (DdlCompileDiagnostic diagnostic in diagnostics)
            Items.Add(new DdlDiagnosticItemViewModel(diagnostic, _focusNodeById));

        RaisePropertyChanged(nameof(ErrorCount));
        RaisePropertyChanged(nameof(WarningCount));
        RaisePropertyChanged(nameof(HasItems));
    }
}

public sealed class DdlDiagnosticItemViewModel
{
    private readonly DdlCompileDiagnostic _diagnostic;

    public DdlDiagnosticItemViewModel(
        DdlCompileDiagnostic diagnostic,
        Action<string>? focusNodeById)
    {
        _diagnostic = diagnostic;

        FocusCommand = new RelayCommand(
            () =>
            {
                if (!string.IsNullOrWhiteSpace(_diagnostic.NodeId))
                    focusNodeById?.Invoke(_diagnostic.NodeId);
            },
            () => !string.IsNullOrWhiteSpace(_diagnostic.NodeId) && focusNodeById is not null
        );
    }

    public string Code => _diagnostic.Code;

    public string Message => _diagnostic.Message;

    public string? NodeId => _diagnostic.NodeId;

    public bool IsWarning => _diagnostic.Severity == DdlDiagnosticSeverity.Warning;

    public bool IsError => _diagnostic.Severity == DdlDiagnosticSeverity.Error;

    public string SeverityLabel => IsError ? "Error" : "Warning";

    public RelayCommand FocusCommand { get; }
}
