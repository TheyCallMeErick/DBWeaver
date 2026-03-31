using Avalonia.Controls;
using Avalonia.Input;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Controls;

public sealed partial class FileVersionHistoryOverlay : UserControl
{
    private FileVersionHistoryViewModel? Vm => DataContext as FileVersionHistoryViewModel;

    public FileVersionHistoryOverlay()
    {
        InitializeComponent();

        Button? closeBtn = this.FindControl<Button>("CloseBtn");
        Button? reloadBtn = this.FindControl<Button>("ReloadBtn");
        Button? restoreBtn = this.FindControl<Button>("RestoreBtn");

        if (closeBtn is not null)
            closeBtn.Click += (_, _) => Vm?.Close();

        if (reloadBtn is not null)
            reloadBtn.Click += async (_, _) =>
            {
                if (Vm is not null)
                    await Vm.ReloadAsync();
            };

        if (restoreBtn is not null)
            restoreBtn.Click += async (_, _) =>
            {
                if (Vm is not null)
                    await Vm.RestoreSelectedAsync();
            };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Vm?.Close();
            e.Handled = true;
        }
    }
}
