using Avalonia.Controls;
using Avalonia.Input;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Controls;

public sealed partial class FlowVersionOverlay : UserControl
{
    private FlowVersionOverlayViewModel? Vm => DataContext as FlowVersionOverlayViewModel;

    public FlowVersionOverlay()
    {
        InitializeComponent();

        Button? closeBtn    = this.FindControl<Button>("CloseBtn");
        Button? saveBtn     = this.FindControl<Button>("SaveBtn");
        Button? diffModeBtn = this.FindControl<Button>("DiffModeBtn");

        if (closeBtn is not null)
            closeBtn.Click += (_, _) => Vm?.Close();

        if (saveBtn is not null)
            saveBtn.Click += (_, _) =>
            {
                if (Vm is null) return;
                Vm.CreateCheckpoint(Vm.NewLabel);
            };

        if (diffModeBtn is not null)
            diffModeBtn.Click += (_, _) =>
            {
                if (Vm is not null)
                    Vm.IsDiffMode = !Vm.IsDiffMode;
            };

        // Wire per-row buttons via event bubbling on the ItemsControl
        this.AddHandler(Button.ClickEvent, OnRowButtonClick);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Avalonia.Input.Key.Escape)
        {
            Vm?.Close();
            e.Handled = true;
        }
    }

    private void OnRowButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Vm is null) return;
        if (e.Source is not Button btn) return;

        // Tag holds the version ID
        string? id = btn.Tag as string;
        if (string.IsNullOrEmpty(id)) return;

        switch (btn.Name)
        {
            case "RestoreBtn":
            {
                var version = Vm.Versions.FirstOrDefault(v => v.Id == id)?.Version;
                if (version is not null)
                    Vm.Restore(version);
                break;
            }
            case "DeleteBtn":
                Vm.DeleteVersion(id);
                break;

            case "CompareBtn":
            {
                var row = Vm.Versions.FirstOrDefault(v => v.Id == id);
                if (row is null) break;
                if (!Vm.IsDiffMode)
                    Vm.IsDiffMode = true;
                if (Vm.DiffBaseVersion is not null && Vm.DiffBaseVersion.Id != id)
                    Vm.ComputeDiff(Vm.DiffBaseVersion.Version, row.Version);
                else
                    Vm.DiffBaseVersion = row;
                break;
            }
        }
    }
}
