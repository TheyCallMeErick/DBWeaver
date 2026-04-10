using Avalonia.Controls;
using DBWeaver.UI.Converters;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Controls;

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
            benchmarkBtn.Click += (_, _) => (DataContext as LiveSqlBarViewModel)?.OpenBenchmark();
        if (explainBtn is not null)
            explainBtn.Click += (_, _) => (DataContext as LiveSqlBarViewModel)?.OpenExplainPlan();
    }

    private async Task CopyToClipboardAsync()
    {
        if (DataContext is not LiveSqlBarViewModel vm)
            return;
        Avalonia.Input.Platform.IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(string.IsNullOrWhiteSpace(vm.DisplaySql) ? vm.RawSql : vm.DisplaySql);
    }
}
