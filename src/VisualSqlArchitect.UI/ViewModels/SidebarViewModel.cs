namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// Represents the three tabs in the left sidebar: Nodes, Connection, and Schema.
/// </summary>
public enum SidebarTab
{
    Nodes,
    Connection,
    Schema
}

/// <summary>
/// ViewModel for the left sidebar containing three tabs: Nodes list, Connection status, and Database schema.
/// Manages tab switching and delegates content to specialized ViewModels.
/// </summary>
public sealed class SidebarViewModel : ViewModelBase
{
    private SidebarTab _activeTab = SidebarTab.Nodes;

    public RelayCommand? SelectNodesCommand { get; set; }
    public RelayCommand? SelectConnectionCommand { get; set; }
    public RelayCommand? SelectSchemaCommand { get; set; }

    /// <summary>
    /// Gets or sets the currently active tab.
    /// </summary>
    public SidebarTab ActiveTab
    {
        get => _activeTab;
        set
        {
            if (Set(ref _activeTab, value))
            {
                RaisePropertyChanged(nameof(ShowNodes));
                RaisePropertyChanged(nameof(ShowConnection));
                RaisePropertyChanged(nameof(ShowSchema));
            }
        }
    }

    /// <summary>
    /// Returns true when Nodes tab is active.
    /// </summary>
    public bool ShowNodes => ActiveTab == SidebarTab.Nodes;

    /// <summary>
    /// Returns true when Connection tab is active.
    /// </summary>
    public bool ShowConnection => ActiveTab == SidebarTab.Connection;

    /// <summary>
    /// Returns true when Schema tab is active.
    /// </summary>
    public bool ShowSchema => ActiveTab == SidebarTab.Schema;

    /// <summary>
    /// ViewModel for the Nodes list tab.
    /// </summary>
    public NodesListViewModel NodesList { get; }

    /// <summary>
    /// ViewModel for the Connection status tab.
    /// </summary>
    public ConnectionManagerViewModel ConnectionManager { get; }

    /// <summary>
    /// ViewModel for the Schema browser tab.
    /// </summary>
    public SchemaViewModel Schema { get; }

    public SidebarViewModel(
        NodesListViewModel nodesList,
        ConnectionManagerViewModel connectionManager,
        SchemaViewModel schema)
    {
        NodesList = nodesList;
        ConnectionManager = connectionManager;
        Schema = schema;
    }
}
