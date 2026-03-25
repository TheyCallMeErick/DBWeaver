using Avalonia.Controls;
using VisualSqlArchitect.UI.Converters;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

public sealed partial class LiveSqlBar : UserControl
{
    public LiveSqlBar()
    {
        InitializeComponent();

        var copyBtn = this.FindControl<Button>("CopyBtn");
        if (copyBtn is not null)
            copyBtn.Click += async (_, _) => await CopyToClipboardAsync();
    }

    private async Task CopyToClipboardAsync()
    {
        if (DataContext is not LiveSqlBarViewModel vm) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(vm.RawSql);
    }
}
