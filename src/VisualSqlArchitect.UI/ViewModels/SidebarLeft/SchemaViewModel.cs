using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using VisualSqlArchitect.Metadata;
using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// Represents a single database object with hierarchical support (table->columns).
/// </summary>
public sealed class SchemaObjectViewModel : ViewModelBase
{
    private bool _isExpanded = false;

    public string Name { get; }
    public string Icon { get; }
    public string SubText { get; }
    public string? DataType { get; }
    public string? BadgeColor { get; }
    public object? Data { get; }
    public ICommand? AddNodeCommand { get; }

    public bool IsExpandable => Children.Count > 0;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public ObservableCollection<SchemaObjectViewModel> Children { get; } = new();

    public SchemaObjectViewModel(string name, string icon, string subText = "", string? dataType = null, string? badgeColor = null, object? data = null, ICommand? addNodeCommand = null)
    {
        Name = name;
        Icon = icon;
        SubText = subText;
        DataType = dataType;
        BadgeColor = badgeColor;
        Data = data;
        AddNodeCommand = addNodeCommand;
    }
}

/// <summary>
/// Represents a category within the schema (Tables, Views, Procedures, Triggers).
/// </summary>
public sealed class SchemaCategoryViewModel : ViewModelBase
{
    private bool _isExpanded = true;

    public string Name { get; }
    public string Icon { get; }
    public string Color { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public ObservableCollection<SchemaObjectViewModel> Items { get; } = new();

    public int Count => Items.Count;

    public RelayCommand<SchemaCategoryViewModel> ToggleCategoryCommand { get; }

    public SchemaCategoryViewModel(string name, string icon, string color)
    {
        Name = name;
        Icon = icon;
        Color = color;
        ToggleCategoryCommand = new RelayCommand<SchemaCategoryViewModel>(category =>
        {
            if (category is not null)
                category.IsExpanded = !category.IsExpanded;
        });
    }
}

/// <summary>
/// ViewModel for the Schema browser tab in the sidebar.
/// Displays database schema: Tables, Views, Procedures, and Triggers.
/// </summary>
public sealed class SchemaViewModel : ViewModelBase
{
    private string _filterQuery = string.Empty;
    private bool _isLoading;
    private bool _hasConnection;
    private DbMetadata? _metadata;

    /// <summary>
    /// The name of the currently connected database.
    /// </summary>
    public string DatabaseName { get; private set; } = "No Connection";

    /// <summary>
    /// The search/filter query to narrow down tables and columns.
    /// </summary>
    public string FilterQuery
    {
        get => _filterQuery;
        set
        {
            if (Set(ref _filterQuery, value))
                Rebuild();
        }
    }

    /// <summary>
    /// True when schema is being loaded from the database.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set => Set(ref _isLoading, value);
    }

    /// <summary>
    /// True when there is an active database connection.
    /// </summary>
    public bool HasConnection
    {
        get => _hasConnection;
        set => Set(ref _hasConnection, value);
    }

    /// <summary>
    /// The database metadata to display.
    /// </summary>
    public DbMetadata? Metadata
    {
        get => _metadata;
        set
        {
            if (Set(ref _metadata, value))
            {
                DatabaseName = value?.DatabaseName ?? "No Connection";
                HasConnection = value is not null;
                Rebuild();
            }
        }
    }

    /// <summary>
    /// Categorized schema items (Tables, Views, Procedures, Triggers).
    /// </summary>
    public ObservableCollection<SchemaCategoryViewModel> Categories { get; } = new();

    private readonly Action<string, IEnumerable<(string name, PinDataType type)>, Point>? _onAddTableNode;

    public SchemaViewModel(Action<string, IEnumerable<(string name, PinDataType type)>, Point>? onAddTableNode = null)
    {
        _onAddTableNode = onAddTableNode;
    }

    private void Rebuild()
    {
        Categories.Clear();

        if (Metadata is null || Metadata.Schemas.Count == 0)
            return;

        // Create category viewmodels - using Material Icon Kind names
        var tablesCategory = new SchemaCategoryViewModel("Tables", "Table", "#60A5FA");
        var viewsCategory = new SchemaCategoryViewModel("Views", "Eye", "#34D399");
        var proceduresCategory = new SchemaCategoryViewModel("Procedures", "CodeBraces", "#FBBF24");
        var triggersCategory = new SchemaCategoryViewModel("Triggers", "Bolt", "#EC4899");

        string filterLower = FilterQuery.ToLower();

        // Collect tables and views
        foreach (var schema in Metadata.Schemas)
        {
            foreach (var table in schema.Tables)
            {
                // Apply filter
                if (!string.IsNullOrEmpty(FilterQuery))
                {
                    var tableMatch = table.Name.ToLower().Contains(filterLower);
                    var schemaMatch = table.Schema?.ToLower().Contains(filterLower) ?? false;
                    var columnMatch = table.Columns.Any(c => c.Name.ToLower().Contains(filterLower));

                    if (!tableMatch && !schemaMatch && !columnMatch)
                        continue;
                }

                var fullName = $"{table.Schema ?? "public"}.{table.Name}";
                var isView = table.Kind != TableKind.Table;
                var category = isView ? viewsCategory : tablesCategory;

                // Create command to add node (for both tables and views)
                ICommand? addNodeCmd = null;
                if (_onAddTableNode is not null)
                {
                    var columns = table.Columns.Select(c =>
                    {
                        // Map ColumnMetadata.DataType to PinDataType
                        var pinType = c.DataType?.ToLower() switch
                        {
                            "int" or "integer" or "bigint" or "smallint" => PinDataType.Number,
                            "varchar" or "text" or "char" or "string" => PinDataType.Text,
                            "bool" or "boolean" or "bit" => PinDataType.Boolean,
                            "decimal" or "numeric" or "float" or "double" => PinDataType.Number,
                            "datetime" or "timestamp" or "date" or "time" => PinDataType.DateTime,
                            "json" or "jsonb" => PinDataType.Json,
                            _ => PinDataType.Any
                        };
                        return (c.Name, pinType);
                    });

                    addNodeCmd = new RelayCommand(() =>
                        _onAddTableNode(fullName, columns, new Point(200, 200))
                    );
                }

                var tableItem = new SchemaObjectViewModel(
                    table.Name,
                    isView ? "Eye" : "Table",
                    fullName,
                    null,
                    null,
                    table,
                    addNodeCmd);

                // Add columns as children (hierarchical)
                foreach (var column in table.Columns)
                {
                    // Determine icon based on key type
                    string colIcon;
                    string badgeColor;

                    if (column.IsPrimaryKey)
                    {
                        colIcon = "KeyPlus";
                        badgeColor = "#FCD34D"; // Yellow
                    }
                    else if (column.IsForeignKey)
                    {
                        colIcon = "LinkVariant";
                        badgeColor = "#F87171"; // Red
                    }
                    else if (column.IsIndexed)
                    {
                        colIcon = "DatabaseSearch";
                        badgeColor = "#60A5FA"; // Blue
                    }
                    else
                    {
                        colIcon = "CircleOutline";
                        badgeColor = "#9CA3AF"; // Gray
                    }

                    var colItem = new SchemaObjectViewModel(
                        column.Name,
                        colIcon,
                        column.DataType ?? "unknown",
                        column.DataType ?? "unknown",
                        badgeColor,
                        column);

                    tableItem.Children.Add(colItem);
                }

                category.Items.Add(tableItem);
            }
        }

        // Add populated categories
        if (tablesCategory.Items.Count > 0)
            Categories.Add(tablesCategory);

        if (viewsCategory.Items.Count > 0)
            Categories.Add(viewsCategory);

        // Add procedures and triggers as empty placeholders for now
        if (Metadata.Schemas.Count > 0)
        {
            proceduresCategory.Items.Add(new SchemaObjectViewModel(
                "Procedures not yet supported", "⚙", "(feature coming soon)"));
            Categories.Add(proceduresCategory);

            triggersCategory.Items.Add(new SchemaObjectViewModel(
                "Triggers not yet supported", "⚡", "(feature coming soon)"));
            Categories.Add(triggersCategory);
        }
    }
}


