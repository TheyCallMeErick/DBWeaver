using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using DBWeaver.Nodes;
using DBWeaver.UI.Serialization;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Controls;

public sealed partial class SearchMenuControl : UserControl
{
    public SearchMenuControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => FocusSearch();

        // Wire mouse clicks on node/table result items
        ItemsControl? list = this.FindControl<ItemsControl>("ResultsList");
        list?.AddHandler(
            PointerPressedEvent,
            OnResultPointerPressed,
            Avalonia.Interactivity.RoutingStrategies.Bubble
        );

        // Wire mouse clicks on snippet items
        ItemsControl? snippetList = this.FindControl<ItemsControl>("SnippetsList");
        snippetList?.AddHandler(
            PointerPressedEvent,
            OnSnippetPointerPressed,
            Avalonia.Interactivity.RoutingStrategies.Bubble
        );
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (DataContext is not SearchMenuViewModel vm)
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
            or Key.Enter when vm.SelectedResult is not null:
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
        if (DataContext is not SearchMenuViewModel vm)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // Walk up the logical tree from the event source to find the NodeSearchResultViewModel
        NodeSearchResultViewModel? result =
            (e.Source as Avalonia.LogicalTree.ILogical)
                ?.GetLogicalAncestors()
                .OfType<Control>()
                .Select(c => c.DataContext)
                .OfType<NodeSearchResultViewModel>()
                .FirstOrDefault()
            ?? (e.Source as Control)?.DataContext as NodeSearchResultViewModel;

        if (result is null)
            return;
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

    private void OnSnippetPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not SearchMenuViewModel vm)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        SnippetViewModel? snippet =
            (e.Source as Avalonia.LogicalTree.ILogical)
                ?.GetLogicalAncestors()
                .OfType<Control>()
                .Select(c => c.DataContext)
                .OfType<SnippetViewModel>()
                .FirstOrDefault()
            ?? (e.Source as Control)?.DataContext as SnippetViewModel;

        if (snippet is null)
            return;
        SnippetRequested?.Invoke(this, snippet.Snippet);
        vm.Close();
        e.Handled = true;
    }

    private void FocusSearch()
    {
        TextBox? input = this.FindControl<TextBox>("SearchInput");
        input?.Focus();
    }

    /// <summary>Raised when the user confirms a node definition selection.</summary>
    public event EventHandler<NodeDefinition>? SpawnRequested;

    /// <summary>Raised when the user selects a table entry.</summary>
    public event EventHandler<(
        string FullName,
        IReadOnlyList<(string Name, PinDataType Type)> Cols
    )>? SpawnTableRequested;

    /// <summary>Raised when the user clicks a saved snippet to insert it.</summary>
    public event EventHandler<SavedSnippet>? SnippetRequested;
}
