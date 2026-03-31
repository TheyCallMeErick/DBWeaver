using Avalonia.Controls;
using VisualSqlArchitect.UI.Converters;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

public sealed partial class LiveSqlBar : UserControl
{
    public LiveSqlBar()
    {
        InitializeComponent();

        Button? copyBtn      = this.FindControl<Button>("CopyBtn");
        Button? formatBtn    = this.FindControl<Button>("FormatBtn");
        Button? benchmarkBtn = this.FindControl<Button>("BenchmarkBtn");
        Button? explainBtn   = this.FindControl<Button>("ExplainBtn");

        if (copyBtn is not null)
            copyBtn.Click += async (_, _) => await CopyToClipboardAsync();
        if (formatBtn is not null)
            formatBtn.Click += (_, _) => (DataContext as LiveSqlBarViewModel)?.FormatSql();
        if (benchmarkBtn is not null)
            benchmarkBtn.Click += (_, _) =>
            {
                if (TopLevel.GetTopLevel(this)?.DataContext is CanvasViewModel canvas)
                    canvas.Benchmark.Open();
            };
        if (explainBtn is not null)
            explainBtn.Click += (_, _) =>
            {
                if (TopLevel.GetTopLevel(this)?.DataContext is CanvasViewModel canvas)
                    canvas.ExplainPlan.Open();
            };
    }

    private async Task CopyToClipboardAsync()
    {
        if (DataContext is not LiveSqlBarViewModel vm)
            return;
        Avalonia.Input.Platform.IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(vm.RawSql);
    }
}
