using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels.Canvas;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.ViewModels;

/// <summary>
/// ViewModel for the Nodes list tab in the sidebar.
/// Displays all available node types grouped by category, ready to spawn on canvas.
/// </summary>
public sealed class NodesListViewModel : ViewModelBase
{
    private string _searchQuery = string.Empty;
    private CanvasContext _canvasContext = CanvasContext.Query;

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
                InitializeGroups(_spawnNode);
                RaisePropertyChanged(nameof(ShowIntro));
                RaisePropertyChanged(nameof(HasResults));
            }
        }
    }

    public CanvasContext CanvasContext
    {
        get => _canvasContext;
        set
        {
            if (!Set(ref _canvasContext, value))
                return;

            // Avoid residual results from another mode context.
            _searchQuery = string.Empty;
            RaisePropertyChanged(nameof(SearchQuery));
            InitializeGroups(_spawnNode);
            RaisePropertyChanged(nameof(ShowIntro));
            RaisePropertyChanged(nameof(HasResults));
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
    public bool HasResults => FilteredGroups.Count > 0;

    private readonly Action<NodeDefinition, Point> _spawnNode;

    public NodesListViewModel(Action<NodeDefinition, Point> spawnNode)
    {
        _spawnNode = spawnNode;
        ClearSearchCommand = new RelayCommand(() => SearchQuery = "");

        // Build initial groups
        InitializeGroups(_spawnNode);
    }

    private void InitializeGroups(Action<NodeDefinition, Point> spawnNode)
    {
        FilteredGroups.Clear();

        if (CanvasContext == CanvasContext.Ddl)
        {
            InitializeDdlGroups(spawnNode);
            RaisePropertyChanged(nameof(HasResults));
            return;
        }

        // Create a dictionary to hold groups as we build them
        var groupsDict = new Dictionary<NodeCategory, NodeTypeGroupViewModel>();

        // Get all node definitions and group by category
        foreach (var def in NodeDefinitionRegistry.All)
        {
            if (!IsDefinitionAllowedInContext(def))
                continue;

            // Filter based on search
            if (!string.IsNullOrEmpty(SearchQuery))
            {
                if (!NodeTagCatalog.MatchesSearch(def, SearchQuery))
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
                defn => spawnNode(defn, new Point(double.NaN, double.NaN))
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

        RaisePropertyChanged(nameof(HasResults));
    }

    private void InitializeDdlGroups(Action<NodeDefinition, Point> spawnNode)
    {
        var groups = new Dictionary<string, NodeTypeGroupViewModel>(StringComparer.OrdinalIgnoreCase);
        var groupOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (NodeDefinition def in NodeDefinitionRegistry.All)
        {
            if (!IsDefinitionAllowedInContext(def))
                continue;

            if (!MatchesSearch(def))
                continue;

            (string key, string name, string color, int order) = ClassifyDdlGroup(def.Type);

            if (!groups.TryGetValue(key, out NodeTypeGroupViewModel? group))
            {
                group = new NodeTypeGroupViewModel(NodeCategory.Ddl, color, name);
                groups[key] = group;
                groupOrder[key] = order;
            }

            group.Items.Add(
                new NodeTypeItemViewModel(def, color, defn => spawnNode(defn, new Point(double.NaN, double.NaN)))
            );
            group.Count = group.Items.Count;
        }

        foreach ((string key, NodeTypeGroupViewModel group) in groups
                     .OrderBy(kv => groupOrder.TryGetValue(kv.Key, out int order) ? order : int.MaxValue)
                     .ThenBy(kv => kv.Value.Name, StringComparer.OrdinalIgnoreCase))
        {
            _ = key;
            FilteredGroups.Add(group);
        }
    }

    private bool MatchesSearch(NodeDefinition def)
    {
        if (string.IsNullOrEmpty(SearchQuery))
            return true;

        return NodeTagCatalog.MatchesSearch(def, SearchQuery);
    }

    private static (string Key, string Name, string Color, int Order) ClassifyDdlGroup(NodeType type)
    {
        return type switch
        {
            NodeType.TableDefinition
                or NodeType.ColumnDefinition
                or NodeType.ViewDefinition
                or NodeType.EnumTypeDefinition
                or NodeType.SequenceDefinition
                or NodeType.ScalarTypeDefinition
                => ("ddl.definitions", "Definitions", UiColorConstants.C_60A5FA, 0),

            NodeType.PrimaryKeyConstraint
                or NodeType.ForeignKeyConstraint
                or NodeType.UniqueConstraint
                or NodeType.CheckConstraint
                or NodeType.DefaultConstraint
                or NodeType.IndexDefinition
                => ("ddl.constraints", "Constraints and Indexes", UiColorConstants.C_A78BFA, 1),

            NodeType.CreateTableOutput
                or NodeType.CreateTypeOutput
                or NodeType.CreateSequenceOutput
                or NodeType.CreateTableAsOutput
                or NodeType.CreateViewOutput
                or NodeType.AlterViewOutput
                or NodeType.AlterTableOutput
                or NodeType.CreateIndexOutput
                => ("ddl.outputs", "Outputs", UiColorConstants.C_22C55E, 2),

            NodeType.AddColumnOp
                or NodeType.DropColumnOp
                or NodeType.RenameColumnOp
                or NodeType.RenameTableOp
                or NodeType.DropTableOp
                or NodeType.AlterColumnTypeOp
                => ("ddl.alter", "Alter Operations", UiColorConstants.C_F59E0B, 3),

            _ => ("ddl.misc", "Other DDL", UiColorConstants.C_94A3B8, 4),
        };
    }

    private static string GetCategoryColor(NodeCategory cat) => cat switch
    {
        NodeCategory.DataSource => UiColorConstants.C_60A5FA,        // Blue
        NodeCategory.StringTransform => UiColorConstants.C_34D399,   // Emerald
        NodeCategory.MathTransform => UiColorConstants.C_FBBF24,     // Amber
        NodeCategory.TypeCast => UiColorConstants.C_A78BFA,          // Violet
        NodeCategory.Comparison => UiColorConstants.C_F87171,        // Red
        NodeCategory.LogicGate => UiColorConstants.C_C084FC,         // Purple
        NodeCategory.Json => UiColorConstants.C_FB923C,              // Orange
        NodeCategory.Aggregate => UiColorConstants.C_14B8A6,         // Teal
        NodeCategory.Conditional => UiColorConstants.C_22D3EE,       // Cyan
        NodeCategory.ResultModifier => UiColorConstants.C_EC4899,    // Pink
        NodeCategory.Output => UiColorConstants.C_10B981,            // Green
        NodeCategory.Literal => UiColorConstants.C_6B7280,           // Gray
        _ => UiColorConstants.C_D1D5DB
    };

    private bool IsDefinitionAllowedInContext(NodeDefinition def)
    {
        return CanvasContext switch
        {
            CanvasContext.Ddl => def.Category == NodeCategory.Ddl,
            CanvasContext.Query or CanvasContext.ViewSubcanvas => def.Category != NodeCategory.Ddl,
            _ => true,
        };
    }
}
