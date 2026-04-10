namespace DBWeaver.UI.ViewModels;

public readonly record struct BenchmarkCommandBindings(
    RelayCommand RunCommand,
    RelayCommand CancelCommand,
    RelayCommand ClearHistoryCommand,
    RelayCommand CloseCommand);
