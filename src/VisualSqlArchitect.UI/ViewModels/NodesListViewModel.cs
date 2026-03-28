using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// Represents a node type available for spawning (from registry).
/// </summary>
public sealed class NodeTypeItemViewModel : ViewModelBase
{
    private bool _isHovered;

    /// <summary>
    /// The node definition (template for creating new instances).
    /// </summary>
    public NodeDefinition Definition { get; }

    /// <summary>
    /// Display title (node type name).
    /// </summary>
    public string Title => Definition.DisplayName;

    /// <summary>
    /// Display subtitle (description).
    /// </summary>
    public string Subtitle => Definition.Description;

    /// <summary>
    /// Color associated with this node's category.
    /// </summary>
    public string Color { get; }

    /// <summary>
    /// Whether user is hovering over this item.
    /// </summary>
    public bool IsHovered
    {
        get => _isHovered;
        set => Set(ref _isHovered, value);
    }

    /// <summary>
    /// Command to spawn this node on the canvas.
    /// </summary>
    public ICommand SpawnNodeCommand { get; }

    public NodeTypeItemViewModel(
        NodeDefinition definition,
        string color,
        Action<NodeDefinition> onSpawn
    )
    {
        Definition = definition;
        Color = color;
        SpawnNodeCommand = new RelayCommand(() => onSpawn(definition));
    }
}

/// <summary>
/// Represents a category group of node types.
/// </summary>
public sealed class NodeTypeGroupViewModel : ViewModelBase
{
    private bool _isExpanded = true;

    /// <summary>
    /// The node category.
    /// </summary>
    public NodeCategory Category { get; }

    /// <summary>
    /// Display name (e.g., "String Functions").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Color associated with this category.
    /// </summary>
    public string Color { get; }

    /// <summary>
    /// Number of node types in this category.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Whether this group is expanded.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    /// <summary>
    /// Command to toggle expand/collapse state.
    /// </summary>
    public ICommand ToggleExpandCommand { get; }

    /// <summary>
    /// Node types in this category.
    /// </summary>
    public ObservableCollection<NodeTypeItemViewModel> Items { get; } = new();

    public NodeTypeGroupViewModel(NodeCategory category, string color)
    {
        Category = category;
        Color = color;
        Name = GetCategoryName(category);
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    private static string GetCategoryName(NodeCategory cat) => cat switch
    {
        NodeCategory.DataSource => "Data Source",
        NodeCategory.StringTransform => "String Functions",
        NodeCategory.MathTransform => "Math Functions",
        NodeCategory.TypeCast => "Type Conversion",
        NodeCategory.Comparison => "Comparisons",
        NodeCategory.LogicGate => "Logic Gates",
        NodeCategory.Json => "JSON Functions",
        NodeCategory.Aggregate => "Aggregates",
        NodeCategory.Conditional => "Conditionals",
        NodeCategory.ResultModifier => "Result Modifiers",
        NodeCategory.Output => "Output",
        NodeCategory.Literal => "Literals",
        _ => cat.ToString()
    };
}

/// <summary>
/// ViewModel for the Nodes list tab in the sidebar.
/// Displays all available node types grouped by category, ready to spawn on canvas.
/// </summary>
public sealed class NodesListViewModel : ViewModelBase
{
    private string _searchQuery = string.Empty;

    /// <summary>
    /// The search query to filter node types.
    /// </summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (Set(ref _searchQuery, value))
            {
                RaisePropertyChanged(nameof(ShowIntro));
            }
        }
    }

    /// <summary>
    /// Whether to show the intro message.
    /// </summary>
    public bool ShowIntro => string.IsNullOrEmpty(SearchQuery);

    /// <summary>
    /// Command to clear the search query.
    /// </summary>
    public ICommand ClearSearchCommand { get; }

    /// <summary>
    /// Filtered and grouped node types, ready for display.
    /// </summary>
    public ObservableCollection<NodeTypeGroupViewModel> FilteredGroups { get; } = new();

    public NodesListViewModel(Action<NodeDefinition, Point> spawnNode)
    {
        ClearSearchCommand = new RelayCommand(() => SearchQuery = "");

        // Build initial groups
        InitializeGroups(spawnNode);
    }

    private void InitializeGroups(Action<NodeDefinition, Point> spawnNode)
    {
        FilteredGroups.Clear();

        // Create a dictionary to hold groups as we build them
        var groupsDict = new Dictionary<NodeCategory, NodeTypeGroupViewModel>();

        // Get all node definitions and group by category
        foreach (var def in NodeDefinitionRegistry.All)
        {
            // Filter based on search
            if (!string.IsNullOrEmpty(SearchQuery))
            {
                var query = SearchQuery.ToLowerInvariant();
                if (!def.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                    !def.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            // Ensure group exists for this category
            if (!groupsDict.TryGetValue(def.Category, out var group))
            {
                var color = GetCategoryColor(def.Category);
                group = new NodeTypeGroupViewModel(def.Category, color);
                groupsDict[def.Category] = group;
            }

            // Add item to group
            var itemVm = new NodeTypeItemViewModel(
                def,
                group.Color,
                defn => spawnNode(defn, new Point(200, 200))
            );
            group.Items.Add(itemVm);
            group.Count = group.Items.Count;
        }

        // Add groups to FilteredGroups in order
        foreach (var cat in Enum.GetValues(typeof(NodeCategory)).Cast<NodeCategory>().OrderBy(c => (int)c))
        {
            if (groupsDict.TryGetValue(cat, out var group) && group.Items.Count > 0)
            {
                FilteredGroups.Add(group);
            }
        }
    }

    private static string GetCategoryColor(NodeCategory cat) => cat switch
    {
        NodeCategory.DataSource => "#60A5FA",        // Blue
        NodeCategory.StringTransform => "#34D399",   // Emerald
        NodeCategory.MathTransform => "#FBBF24",     // Amber
        NodeCategory.TypeCast => "#A78BFA",          // Violet
        NodeCategory.Comparison => "#F87171",        // Red
        NodeCategory.LogicGate => "#C084FC",         // Purple
        NodeCategory.Json => "#FB923C",              // Orange
        NodeCategory.Aggregate => "#14B8A6",         // Teal
        NodeCategory.Conditional => "#22D3EE",       // Cyan
        NodeCategory.ResultModifier => "#EC4899",    // Pink
        NodeCategory.Output => "#10B981",            // Green
        NodeCategory.Literal => "#6B7280",           // Gray
        _ => "#D1D5DB"
    };
}
