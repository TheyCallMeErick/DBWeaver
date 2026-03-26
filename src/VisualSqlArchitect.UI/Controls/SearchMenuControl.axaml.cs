using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

public sealed partial class SearchMenuControl : UserControl
{
    public SearchMenuControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => FocusSearch();

        // Wire mouse clicks on result items
        var list = this.FindControl<ItemsControl>("ResultsList");
        if (list is not null)
            list.AddHandler(PointerPressedEvent, OnResultPointerPressed, Avalonia.Interactivity.RoutingStrategies.Bubble);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (DataContext is not SearchMenuViewModel vm) return;

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

            case Key.Return or Key.Enter when vm.SelectedResult is not null:
                SpawnResult(vm.SelectedResult, vm);
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
        if (DataContext is not SearchMenuViewModel vm) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        // Walk up the logical tree from the event source to find the NodeSearchResultViewModel
        var result = (e.Source as Avalonia.LogicalTree.ILogical)?
            .GetLogicalAncestors()
            .OfType<Control>()
            .Select(c => c.DataContext)
            .OfType<NodeSearchResultViewModel>()
            .FirstOrDefault()
            ?? (e.Source as Control)?.DataContext as NodeSearchResultViewModel;

        if (result is null) return;
        vm.SelectedResult = result;
        SpawnResult(result, vm);
        e.Handled = true;
    }

    private void SpawnResult(NodeSearchResultViewModel result, SearchMenuViewModel vm)
    {
        if (result.IsTable)
            SpawnTableRequested?.Invoke(this, (result.TableFullName, result.TableColumns));
        else
            SpawnRequested?.Invoke(this, result.Definition);
        vm.Close();
    }

    private void FocusSearch()
    {
        var input = this.FindControl<TextBox>("SearchInput");
        input?.Focus();
    }

    /// <summary>Raised when the user confirms a node definition selection.</summary>
    public event EventHandler<NodeDefinition>? SpawnRequested;

    /// <summary>Raised when the user selects a table entry.</summary>
    public event EventHandler<(string FullName, IReadOnlyList<(string Name, PinDataType Type)> Cols)>? SpawnTableRequested;
}
