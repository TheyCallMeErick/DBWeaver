using Avalonia.Controls;
using Avalonia.Input;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Controls;

public sealed partial class ExplainPlanOverlay : UserControl
{
    public ExplainPlanOverlay()
    {
        InitializeComponent();

        Button? closeBtn = this.FindControl<Button>("CloseBtn");
        Button? runBtn   = this.FindControl<Button>("RunBtn");

        if (closeBtn is not null)
            closeBtn.Click += (_, _) => (DataContext as ExplainPlanViewModel)?.Close();

        if (runBtn is not null)
            runBtn.Click += async (_, _) =>
            {
                if (DataContext is ExplainPlanViewModel vm)
                    await vm.RunExplainAsync();
            };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && DataContext is ExplainPlanViewModel vm)
        {
            vm.Close();
            e.Handled = true;
        }
    }
}
