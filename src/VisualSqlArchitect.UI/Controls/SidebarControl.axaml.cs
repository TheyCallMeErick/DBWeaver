using Avalonia;
using Avalonia.Controls;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

public partial class SidebarControl : UserControl
{
    private bool _buttonsWired = false;

    public SidebarControl()
    {
        InitializeComponent();

        // Wire up button click handlers when loaded
        this.Loaded += (_, _) => WireUpButtons();
    }

    private void WireUpButtons()
    {
        if (_buttonsWired || DataContext is not SidebarViewModel vm)
            return;

        _buttonsWired = true;

        var nodesButton = this.FindControl<Button>("NodesTabButton");
        var connectionButton = this.FindControl<Button>("ConnectionTabButton");
        var schemaButton = this.FindControl<Button>("SchemaTabButton");

        if (nodesButton != null)
        {
            nodesButton.Click += (_, _) => vm.ActiveTab = SidebarTab.Nodes;
        }
        if (connectionButton != null)
        {
            connectionButton.Click += (_, _) => vm.ActiveTab = SidebarTab.Connection;
        }
        if (schemaButton != null)
        {
            schemaButton.Click += (_, _) => vm.ActiveTab = SidebarTab.Schema;
        }

        // Set child control DataContexts
        var nodesControl = this.FindControl<NodesListControl>("NodesControl");
        var connectionControl = this.FindControl<ConnectionTabControl>("ConnectionControl");
        var schemaControl = this.FindControl<SchemaControl>("SchemaControl");

        if (nodesControl != null)
            nodesControl.DataContext = vm.NodesList;
        if (connectionControl != null)
            connectionControl.DataContext = vm.ConnectionManager;
        if (schemaControl != null)
            schemaControl.DataContext = vm.Schema;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        // Reset and re-wire if data context changes
        if (this.IsLoaded)
        {
            _buttonsWired = false;
            WireUpButtons();
        }
    }
}
