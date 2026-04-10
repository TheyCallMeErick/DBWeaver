namespace DBWeaver.UI.ViewModels;

public static class BenchmarkCommandFactory
{
    public static BenchmarkCommandBindings Create(
        Action startRun,
        Func<bool> canRun,
        Action cancel,
        Func<bool> canCancel,
        Action clearHistory,
        Action close)
    {
        return new BenchmarkCommandBindings(
            RunCommand: new RelayCommand(startRun, canRun),
            CancelCommand: new RelayCommand(cancel, canCancel),
            ClearHistoryCommand: new RelayCommand(clearHistory),
            CloseCommand: new RelayCommand(close)
        );
    }
}
