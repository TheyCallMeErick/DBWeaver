using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

public sealed partial class CommandPaletteControl : UserControl
{
    public CommandPaletteControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => FocusSearch();

        var list = this.FindControl<ItemsControl>("ResultsList");
        if (list is not null)
            list.AddHandler(PointerPressedEvent, OnResultPointerPressed, Avalonia.Interactivity.RoutingStrategies.Bubble);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (DataContext is not CommandPaletteViewModel vm) return;

        switch (e.Key)
        {
            case Key.Down:
                vm.SelectNext();
                e.Handled = true;
                break;

            case Key.Up:
                vm.SelectPrev();
                e.Handled = true;
                break;

            case Key.Return or Key.Enter:
                vm.ExecuteSelected();
                e.Handled = true;
                break;

            case Key.Escape:
                vm.Close();
                e.Handled = true;
                break;
        }
    }

    private void OnResultPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not CommandPaletteViewModel vm) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var item = (e.Source as Avalonia.LogicalTree.ILogical)?
            .GetLogicalAncestors()
            .OfType<Control>()
            .Select(c => c.DataContext)
            .OfType<PaletteCommandItem>()
            .FirstOrDefault()
            ?? (e.Source as Control)?.DataContext as PaletteCommandItem;

        if (item is null) return;

        var idx = vm.Results.IndexOf(item);
        if (idx >= 0) vm.SelectedIndex = idx;
        vm.ExecuteSelected();
        e.Handled = true;
    }

    private void FocusSearch()
    {
        var input = this.FindControl<TextBox>("SearchInput");
        input?.Focus();
    }
}
