using Avalonia.Controls;
using Avalonia.Input;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

public sealed partial class SearchMenuControl : UserControl
{
    public SearchMenuControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => FocusSearch();
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
                // Spawn the selected node — bubble up via message or event
                SpawnRequested?.Invoke(this, vm.SelectedResult.Definition);
                vm.Close();
                e.Handled = true;
                break;

            case Key.Escape:
                vm.Close();
                e.Handled = true;
                break;
        }
    }

    private void FocusSearch()
    {
        // Auto-focus the search TextBox when the menu becomes visible
        var input = this.FindControl<TextBox>("SearchInput");
        input?.Focus();
    }

    /// <summary>Raised when the user confirms a node selection (Enter).</summary>
    public event EventHandler<Nodes.NodeDefinition>? SpawnRequested;
}
