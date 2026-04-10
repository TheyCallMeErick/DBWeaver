using Avalonia.Media;
using Material.Icons;
using DBWeaver.Nodes;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.ViewModels.Canvas;

/// <summary>
/// Represents a search result item (either a node definition or a database table).
/// Used in SearchMenuViewModel for displaying available nodes and tables.
/// </summary>
public sealed class NodeSearchResultViewModel : ViewModelBase
{
    private readonly NodeDefinition? _def;

    /// <summary>
    /// Constructor for node-based results.
    /// </summary>
    public NodeSearchResultViewModel(NodeDefinition def)
    {
        _def = def;
    }

    /// <summary>
    /// Private parameterless constructor for table-based results.
    /// Use ForTable() factory method to create table results.
    /// </summary>
    private NodeSearchResultViewModel() { }

    // ═══════════════════════════════════════════════════════════════════════════════
    // TABLE-SPECIFIC FIELDS
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Whether this result represents a database table (vs a node definition).
    /// </summary>
    public bool IsTable { get; init; }

    /// <summary>
    /// Full qualified table name (e.g., "dbo.Products").
    /// Only populated if IsTable is true.
    /// </summary>
    public string TableFullName { get; init; } = "";

    /// <summary>
    /// List of column name and type pairs for the table.
    /// Only populated if IsTable is true.
    /// </summary>
    public IReadOnlyList<(string Name, PinDataType Type)> TableColumns { get; init; } = [];

    /// <summary>
    /// Factory method to create a table-based search result.
    /// </summary>
    /// <param name="fullName">Full qualified table name</param>
    /// <param name="cols">Columns of the table</param>
    /// <returns>A new NodeSearchResultViewModel representing the table</returns>
    public static NodeSearchResultViewModel ForTable(
        string fullName,
        IReadOnlyList<(string Name, PinDataType Type)> cols
    ) =>
        new()
        {
            IsTable = true,
            TableFullName = fullName,
            TableColumns = cols,
        };

    // ═══════════════════════════════════════════════════════════════════════════════
    // SHARED VIEW PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The underlying node definition (or default if this is a table result).
    /// </summary>
    public NodeDefinition Definition => _def ?? NodeDefinitionRegistry.Get(NodeType.TableSource);

    /// <summary>
    /// Display title: table name for tables, DisplayName for nodes.
    /// </summary>
    public string Title => IsTable ? TableFullName.Split('.').Last() : _def!.DisplayName;

    /// <summary>
    /// Category: "Table" for tables, Category name for nodes.
    /// </summary>
    public string Category => IsTable ? "Table" : _def!.Category.ToString();

    /// <summary>
    /// Icon resource name for the result.
    /// </summary>
    public string Icon => NodeIconCatalog.GetForCategory(_def?.Category ?? NodeCategory.DataSource);

    /// <summary>
    /// Material Design icon kind for the result.
    /// </summary>
    public MaterialIconKind IconKind =>
        NodeIconCatalog.GetKindForCategory(_def?.Category ?? NodeCategory.DataSource);

    /// <summary>
    /// Accent color for the result in the UI.
    /// Tables use Teal, nodes use category-specific colors.
    /// </summary>
    public Color AccentColor =>
        IsTable
            ? Color.Parse(UiColorConstants.C_14B8A6)
            : _def!.Category switch
            {
                NodeCategory.DataSource => Color.Parse(UiColorConstants.C_14B8A6),
                NodeCategory.StringTransform => Color.Parse(UiColorConstants.C_818CF8),
                NodeCategory.MathTransform => Color.Parse(UiColorConstants.C_FBBF24),
                NodeCategory.TypeCast => Color.Parse(UiColorConstants.C_C084FC),
                NodeCategory.Comparison => Color.Parse(UiColorConstants.C_FB7185),
                NodeCategory.LogicGate => Color.Parse(UiColorConstants.C_FB923C),
                NodeCategory.Json => Color.Parse(UiColorConstants.C_A78BFA),
                NodeCategory.Aggregate => Color.Parse(UiColorConstants.C_4ADE80),
                _ => Color.Parse(UiColorConstants.C_9CA3AF),
            };

    /// <summary>
    /// Solid color brush for the accent color.
    /// </summary>
    public SolidColorBrush AccentBrush => new(AccentColor);
}
