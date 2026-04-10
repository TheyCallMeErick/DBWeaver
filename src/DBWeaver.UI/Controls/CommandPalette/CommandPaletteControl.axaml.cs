using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Controls;

public sealed partial class CommandPaletteControl : UserControl
{
    private CommandPaletteViewModel? _vm;

    public CommandPaletteControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => BindViewModel();

        ItemsControl? list = this.FindControl<ItemsControl>("ResultsList");
        list?.AddHandler(
            PointerPressedEvent,
            OnResultPointerPressed,
            Avalonia.Interactivity.RoutingStrategies.Bubble
        );
    }

    private void BindViewModel()
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as CommandPaletteViewModel;

        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommandPaletteViewModel.IsVisible) && _vm?.IsVisible == true)
            Dispatcher.UIThread.Post(FocusSearch, DispatcherPriority.Input);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (DataContext is not CommandPaletteViewModel vm)
            return;

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

            case Key.Return
            or Key.Enter:
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
        if (DataContext is not CommandPaletteViewModel vm)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        PaletteCommandItem? item =
            (e.Source as Avalonia.LogicalTree.ILogical)
                ?.GetLogicalAncestors()
                .OfType<Control>()
                .Select(c => c.DataContext)
                .OfType<PaletteCommandItem>()
                .FirstOrDefault()
            ?? (e.Source as Control)?.DataContext as PaletteCommandItem;

        if (item is null)
            return;

        int idx = vm.Results.IndexOf(item);
        if (idx >= 0)
            vm.SelectedIndex = idx;
        vm.ExecuteSelected();
        e.Handled = true;
    }

    private void FocusSearch()
    {
        TextBox? input = this.FindControl<TextBox>("SearchInput");
        input?.Focus();
    }
}
