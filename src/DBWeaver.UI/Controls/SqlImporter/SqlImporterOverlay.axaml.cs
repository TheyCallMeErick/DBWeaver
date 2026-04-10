using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Controls;

public sealed partial class SqlImporterOverlay : UserControl
{
    public SqlImporterOverlay()
    {
        InitializeComponent();

        Button? closeBtn = this.FindControl<Button>("CloseBtn");
        Button? importBtn = this.FindControl<Button>("ImportBtn");
        Button? confirmClearCanvasBtn = this.FindControl<Button>("ConfirmClearCanvasBtn");
        Button? cancelClearCanvasBtn = this.FindControl<Button>("CancelClearCanvasBtn");

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

        if (confirmClearCanvasBtn is not null)
            confirmClearCanvasBtn.Click += async (_, _) =>
            {
                if (DataContext is SqlImporterViewModel vm)
                    await vm.ConfirmClearCanvasAndImportAsync();
            };

        if (cancelClearCanvasBtn is not null)
            cancelClearCanvasBtn.Click += (_, _) =>
            {
                if (DataContext is SqlImporterViewModel vm)
                    vm.CancelClearCanvasConfirmation();
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

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SqlImporterViewModel vm)
            vm.Close();
    }
}
