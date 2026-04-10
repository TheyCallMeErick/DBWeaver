using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Controls.Start;

public partial class StartMenuControl : UserControl
{
    private const double CompactBreakpoint = 1400;
    private const double LargeBreakpoint = 1850;
    private bool _entryAnimationPlayed;

    public StartMenuControl()
    {
        InitializeComponent();

        AttachedToVisualTree += (_, _) =>
        {
            ApplySizeClasses(Bounds.Width);
            if (_entryAnimationPlayed)
                return;

            _entryAnimationPlayed = true;
            _ = RunEntryAnimationAsync();
        };
        SizeChanged += (_, e) => ApplySizeClasses(e.NewSize.Width);
    }

    private StartMenuViewModel? Vm => DataContext as StartMenuViewModel;

    private void RecentProjectCard_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || sender is not Button { Tag: StartRecentProjectItem item })
            return;

        Vm.OpenRecentProjectCommand.Execute(item);
    }

    private void TemplateCard_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || sender is not Button { Tag: StartTemplateItem item })
            return;

        Vm.OpenTemplateCommand.Execute(item);
    }

    private void TemplateFavorite_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || sender is not Button { Tag: StartTemplateItem item })
            return;

        Vm.ToggleTemplateFavoriteCommand.Execute(item);
        e.Handled = true;
    }

    private void SavedConnectionCard_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || sender is not Button { Tag: StartSavedConnectionItem item })
            return;

        Vm.OpenSavedConnectionCommand.Execute(item);
    }

    private void ApplySizeClasses(double width)
    {
        bool compact = width > 0 && width <= CompactBreakpoint;
        bool large = width >= LargeBreakpoint;

        Classes.Set("start-compact", compact);
        Classes.Set("start-large", large);
    }

    private async Task RunEntryAnimationAsync()
    {
        var targets = new Control?[]
        {
            this.FindControl<Control>("HeroBlock"),
            this.FindControl<Control>("RecentSection"),
            this.FindControl<Control>("ConnectionsSection"),
            this.FindControl<Control>("TemplatesSection"),
        };

        foreach (Control? target in targets)
        {
            if (target is null)
                continue;

            target.Opacity = 0;
        }

        foreach (Control? target in targets)
        {
            if (target is null)
                continue;

            await Task.Delay(45);
            await FadeInAsync(target);
        }
    }

    private static async Task FadeInAsync(Control target)
    {
        const int steps = 6;
        for (int i = 1; i <= steps; i++)
        {
            target.Opacity = i / (double)steps;
            await Task.Delay(24);
        }
    }
}
