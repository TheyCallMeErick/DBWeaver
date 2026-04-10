using System.Windows.Input;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorCommandNotifier
{
    public void NotifyMutationCommands(ICommand confirmPendingMutationCommand, ICommand cancelPendingMutationCommand)
    {
        (confirmPendingMutationCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (cancelPendingMutationCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    public void NotifyTabCommands(
        ICommand newTabCommand,
        ICommand closeTabCommand,
        ICommand closeActiveTabCommand,
        ICommand confirmPendingCloseTabCommand,
        ICommand cancelPendingCloseTabCommand)
    {
        (newTabCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (closeTabCommand as RelayCommand<string>)?.NotifyCanExecuteChanged();
        (closeActiveTabCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (confirmPendingCloseTabCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (cancelPendingCloseTabCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    public void NotifyAll(
        ICommand confirmPendingMutationCommand,
        ICommand cancelPendingMutationCommand,
        ICommand newTabCommand,
        ICommand closeTabCommand,
        ICommand closeActiveTabCommand,
        ICommand confirmPendingCloseTabCommand,
        ICommand cancelPendingCloseTabCommand)
    {
        NotifyMutationCommands(confirmPendingMutationCommand, cancelPendingMutationCommand);
        NotifyTabCommands(
            newTabCommand,
            closeTabCommand,
            closeActiveTabCommand,
            confirmPendingCloseTabCommand,
            cancelPendingCloseTabCommand);
    }
}

