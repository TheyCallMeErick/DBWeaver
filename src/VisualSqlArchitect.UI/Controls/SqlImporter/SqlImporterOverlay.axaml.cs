using Avalonia.Controls;
using Avalonia.Input;
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
            closeBtn.Click += (_, _) => (DataContext as SqlImporterViewModel)?.Close();

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
            vm.Close();
            e.Handled = true;
        }
    }
}
