using System.Windows.Input;
using DBWeaver.UI.Services.SqlEditor;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlEditorCommandNotifierTests
{
    [Fact]
    public void NotifyMutationCommands_WithRelayCommands_DoesNotThrow()
    {
        var sut = new SqlEditorCommandNotifier();
        ICommand confirm = new RelayCommand(() => { });
        ICommand cancel = new RelayCommand(() => { });

        sut.NotifyMutationCommands(confirm, cancel);
    }

    [Fact]
    public void NotifyTabCommands_WithRelayCommands_DoesNotThrow()
    {
        var sut = new SqlEditorCommandNotifier();
        ICommand newTab = new RelayCommand(() => { });
        ICommand closeTab = new RelayCommand<string>(_ => { });
        ICommand closeActive = new RelayCommand(() => { });
        ICommand confirmClose = new RelayCommand(() => { });
        ICommand cancelClose = new RelayCommand(() => { });

        sut.NotifyTabCommands(newTab, closeTab, closeActive, confirmClose, cancelClose);
    }

    [Fact]
    public void NotifyAll_WithNonRelayCommands_DoesNotThrow()
    {
        var sut = new SqlEditorCommandNotifier();
        ICommand fake = new FakeCommand();

        sut.NotifyAll(fake, fake, fake, fake, fake, fake, fake);
    }

    private sealed class FakeCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }
}

