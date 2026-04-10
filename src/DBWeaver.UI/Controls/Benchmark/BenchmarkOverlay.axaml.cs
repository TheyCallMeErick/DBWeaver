using Avalonia.Controls;
using Avalonia.Input;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Controls;

public sealed partial class BenchmarkOverlay : UserControl
{
    public BenchmarkOverlay()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && DataContext is BenchmarkViewModel vm)
        {
            vm.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }
}
