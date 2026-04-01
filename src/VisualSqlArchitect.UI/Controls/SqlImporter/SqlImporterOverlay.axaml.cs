using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Controls;

public sealed partial class SqlImporterOverlay : UserControl
{
    public SqlImporterOverlay()
    {
        InitializeComponent();

        Button? closeBtn = this.FindControl<Button>("CloseBtn");
        Button? importBtn = this.FindControl<Button>("ImportBtn");

        if (closeBtn is not null)
            closeBtn.Click += (_, _) =>
            {
                if (DataContext is not SqlImporterViewModel vm)
                    return;

                if (vm.IsImporting)
                    vm.CancelImport();
                else
                    vm.Close();
            };

        if (importBtn is not null)
            importBtn.Click += async (_, _) =>
            {
                if (DataContext is SqlImporterViewModel vm)
                    await vm.ImportAsync();
            };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && DataContext is SqlImporterViewModel vm)
        {
            if (vm.IsImporting)
                vm.CancelImport();
            else
                vm.Close();

            e.Handled = true;
        }
    }

    private void OnReportItemClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SqlImporterViewModel vm)
            return;

        if (sender is not Button button || button.Tag is not ImportReportItem item)
            return;

        vm.FocusReportItem(item);
    }
}
