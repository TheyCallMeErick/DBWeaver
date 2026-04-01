using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Material.Icons;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Services.Localization;
using VisualSqlArchitect.UI.ViewModels.UndoRedo.Commands;

namespace VisualSqlArchitect.UI.ViewModels;

// ─── Parameter row ────────────────────────────────────────────────────────────

/// <summary>
/// A single editable parameter row in the property panel.
/// Bound to one <see cref="NodeParameter"/> on the selected node.
/// </summary>
public sealed class ParameterRowViewModel(NodeParameter param, string? currentValue) : ViewModelBase
{
    private string? _value = currentValue ?? param.DefaultValue;
    private bool _isDirty;

    public string Name { get; } = param.Name;
    public ParameterKind Kind { get; } = param.Kind;
    public string? Description { get; } = param.Description;
    public IReadOnlyList<string>? EnumValues { get; } = param.EnumValues;

    // ── Visibility helpers (one True per kind) ────────────────────────────────
    public bool IsText => Kind is ParameterKind.Text or ParameterKind.JsonPath;
    public bool IsNumber => Kind == ParameterKind.Number;
    public bool IsBoolean => Kind == ParameterKind.Boolean;
    public bool IsEnum => Kind is ParameterKind.Enum or ParameterKind.CastType;
    public bool IsDateTime => Kind == ParameterKind.DateTime;
    public bool IsDate => Kind == ParameterKind.Date;
    public string? Value
    {
        get => _value;
        set
        {
            if (Set(ref _value, value))
                IsDirty = true;
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set => Set(ref _isDirty, value);
    }

    public void MarkClean() => IsDirty = false;
}

// ─── Pin info row ─────────────────────────────────────────────────────────────

public sealed class PinInfoRowViewModel(PinViewModel pin)
{
    public string Name => pin.Name;
    public string TypeLabel => pin.DataType.ToString();
    public string Direction => pin.Direction.ToString();
    public bool Connected => pin.IsConnected;
    public Avalonia.Media.Color Color => pin.PinColor;
    public Avalonia.Media.SolidColorBrush ColorBrush => pin.PinBrush;
}

// ─── Property panel ──────────────────────────────────────────────────────────

/// <summary>
/// Bound to the right-side panel. Shows details and editable parameters for
/// the currently selected node, or a multi-selection summary.
/// </summary>
public sealed class PropertyPanelViewModel : ViewModelBase
{
    private NodeViewModel? _selectedNode;
    private bool _isVisible;
    private string _panelTitle = "Properties";
    private string _lastRawSql = string.Empty;
    private string? _sqlTraceFragment;
    private string? _sqlTraceContext;

    private readonly UndoRedoStack _undo;
    private readonly LocalizationService _loc = LocalizationService.Instance;

    public PropertyPanelViewModel(UndoRedoStack undo)
    {
        _undo = undo;
        _loc.PropertyChanged += (_, _) =>
        {
            RaisePropertyChanged(nameof(NodeAliasLabel));
        };
    }

    // ── Sub-collections ───────────────────────────────────────────────────────
    public ObservableCollection<ParameterRowViewModel> Parameters { get; } = [];
    public ObservableCollection<PinInfoRowViewModel> InputPins { get; } = [];
    public ObservableCollection<PinInfoRowViewModel> OutputPins { get; } = [];

    // ── State ─────────────────────────────────────────────────────────────────

    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        private set => Set(ref _selectedNode, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    public string PanelTitle
    {
        get => _panelTitle;
        private set => Set(ref _panelTitle, value);
    }

    // ── SQL Trace ─────────────────────────────────────────────────────────────

    public string? SqlTraceFragment
    {
        get => _sqlTraceFragment;
        private set => Set(ref _sqlTraceFragment, value);
    }

    public string? SqlTraceContext
    {
        get => _sqlTraceContext;
        private set => Set(ref _sqlTraceContext, value);
    }

    public bool HasSqlTrace => SqlTraceFragment is not null;

    /// <summary>
    /// Called by CanvasViewModel whenever the live SQL output changes.
    /// Stores the latest SQL and recomputes the trace for the selected node.
    /// </summary>
    public void UpdateSqlTrace(string rawSql)
    {
        _lastRawSql = rawSql ?? string.Empty;
        RecomputeTrace();
    }

    private void RecomputeTrace()
    {
        if (SelectedNode is null || string.IsNullOrWhiteSpace(_lastRawSql))
        {
            SqlTraceFragment = null;
            SqlTraceContext = null;
            RaisePropertyChanged(nameof(HasSqlTrace));
            return;
        }
        (SqlTraceContext, SqlTraceFragment) = ExtractTrace(SelectedNode, _lastRawSql);
        RaisePropertyChanged(nameof(HasSqlTrace));
    }

    // ── Computed from SelectedNode ────────────────────────────────────────────

    public bool HasNode => SelectedNode is not null;
    public bool HasParams => Parameters.Count > 0;
    public bool HasInputs => InputPins.Count > 0;
    public bool HasOutputs => OutputPins.Count > 0;

    public string NodeTitle => SelectedNode?.Title ?? string.Empty;
    public string NodeCategory => SelectedNode?.Category.ToString() ?? string.Empty;
    public string NodeAlias
    {
        get => SelectedNode?.Alias ?? string.Empty;
        set
        {
            if (SelectedNode is not null)
                SelectedNode.Alias = string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    public string NodeAliasLabel =>
        IsSourceAliasNode(SelectedNode?.Type)
            ? _loc["property.sourceAlias"]
            : _loc["property.outputAlias"];

    /// <summary>
    /// True when alias editing is meaningful for the selected node type.
    /// Hides the alias input for structural/predicate/output nodes where alias has no effect.
    /// </summary>
    public bool ShowAliasEditor => SelectedNode is not null && SupportsAliasEditor(SelectedNode.Type);

    public Avalonia.Media.LinearGradientBrush? HeaderGradient => SelectedNode?.HeaderGradient;

    public string CategoryIcon => SelectedNode?.CategoryIcon ?? string.Empty;
    public MaterialIconKind CategoryIconKind =>
        SelectedNode?.CategoryIconKind ?? MaterialIconKind.Help;

    // ── Selection management ──────────────────────────────────────────────────

    public void ShowNode(NodeViewModel node)
    {
        // Commit any dirty parameters before switching
        CommitDirty();

        SelectedNode = node;
        PanelTitle = node.Title;
        IsVisible = true;

        RebuildRows(node);
        RecomputeTrace();
        RaisePropertyChanged(nameof(HasNode));
        RaisePropertyChanged(nameof(HasParams));
        RaisePropertyChanged(nameof(HasInputs));
        RaisePropertyChanged(nameof(HasOutputs));
        RaisePropertyChanged(nameof(NodeTitle));
        RaisePropertyChanged(nameof(NodeCategory));
        RaisePropertyChanged(nameof(NodeAlias));
        RaisePropertyChanged(nameof(NodeAliasLabel));
        RaisePropertyChanged(nameof(ShowAliasEditor));
        RaisePropertyChanged(nameof(HeaderGradient));
        RaisePropertyChanged(nameof(CategoryIcon));
        RaisePropertyChanged(nameof(CategoryIconKind));
    }

    public void ShowMultiSelection(IReadOnlyList<NodeViewModel> nodes)
    {
        CommitDirty();
        SelectedNode = null;
        PanelTitle = $"{nodes.Count} nodes selected";
        Parameters.Clear();
        InputPins.Clear();
        OutputPins.Clear();
        SqlTraceFragment = null;
        SqlTraceContext = null;
        RaisePropertyChanged(nameof(HasSqlTrace));
        IsVisible = true;
        RaisePropertyChanged(nameof(HasNode));
        RaisePropertyChanged(nameof(HasParams));
        RaisePropertyChanged(nameof(HasInputs));
        RaisePropertyChanged(nameof(HasOutputs));
        RaisePropertyChanged(nameof(NodeAliasLabel));
        RaisePropertyChanged(nameof(ShowAliasEditor));
    }

    public void Clear()
    {
        CommitDirty();
        SelectedNode = null;
        PanelTitle = "Properties";
        Parameters.Clear();
        InputPins.Clear();
        OutputPins.Clear();
        SqlTraceFragment = null;
        SqlTraceContext = null;
        RaisePropertyChanged(nameof(HasSqlTrace));
        IsVisible = false;
        RaisePropertyChanged(nameof(HasNode));
        RaisePropertyChanged(nameof(HasParams));
        RaisePropertyChanged(nameof(HasInputs));
        RaisePropertyChanged(nameof(HasOutputs));
        RaisePropertyChanged(nameof(NodeAliasLabel));
        RaisePropertyChanged(nameof(ShowAliasEditor));
    }

    private static bool IsSourceAliasNode(NodeType? type) =>
        type is NodeType.TableSource or NodeType.Subquery or NodeType.CteSource;

    private static bool SupportsAliasEditor(NodeType type) => type switch
    {
        // Source aliases
        NodeType.TableSource or NodeType.Subquery or NodeType.CteSource => true,

        // Explicit alias / scalar transforms / computed expressions
        NodeType.Alias
            or NodeType.Upper or NodeType.Lower or NodeType.Trim or NodeType.Substring
            or NodeType.RegexMatch or NodeType.RegexReplace or NodeType.RegexExtract
            or NodeType.Concat or NodeType.StringLength or NodeType.Replace
            or NodeType.Round or NodeType.Abs or NodeType.Ceil or NodeType.Floor
            or NodeType.Add or NodeType.Subtract or NodeType.Multiply or NodeType.Divide
            or NodeType.Modulo or NodeType.DateAdd or NodeType.DateDiff or NodeType.DatePart
            or NodeType.DateFormat
            or NodeType.CountStar or NodeType.CountDistinct or NodeType.Sum or NodeType.Avg
            or NodeType.Min or NodeType.Max or NodeType.StringAgg or NodeType.WindowFunction
            or NodeType.Cast or NodeType.ColumnRefCast or NodeType.ScalarFromColumn
            or NodeType.JsonExtract or NodeType.JsonValue or NodeType.JsonArrayLength
            or NodeType.Case or NodeType.NullFill or NodeType.EmptyFill or NodeType.ValueMap
            or NodeType.ValueNumber or NodeType.ValueString or NodeType.ValueDateTime
            or NodeType.ValueBoolean or NodeType.SystemDate or NodeType.SystemDateTime
            or NodeType.CurrentDate or NodeType.CurrentTime => true,

        _ => false,
    };

    // ── Parameter building ────────────────────────────────────────────────────

    private void RebuildRows(NodeViewModel node)
    {
        Parameters.Clear();
        InputPins.Clear();
        OutputPins.Clear();

        // Get the static definition for this node type
        NodeDefinition? def = null;
        try
        {
            def = NodeDefinitionRegistry.Get(node.Type);
        }
        catch
        { /* TableSource and custom nodes have no registry entry */
        }

        if (def is not null)
        {
            foreach (NodeParameter param in def.Parameters)
            {
                node.Parameters.TryGetValue(param.Name, out string? currentVal);
                Parameters.Add(new ParameterRowViewModel(param, currentVal));
            }
        }

        foreach (PinViewModel pin in node.InputPins)
            InputPins.Add(new PinInfoRowViewModel(pin));

        foreach (PinViewModel pin in node.OutputPins)
            OutputPins.Add(new PinInfoRowViewModel(pin));
    }

    // ── Commit / apply ────────────────────────────────────────────────────────

    /// <summary>
    /// Writes all dirty parameter rows to the node via undo-able commands.
    /// Called automatically on selection change and on explicit Apply.
    /// </summary>
    public void CommitDirty()
    {
        if (SelectedNode is null)
            return;

        foreach (ParameterRowViewModel? row in Parameters.Where(r => r.IsDirty))
        {
            SelectedNode.Parameters.TryGetValue(row.Name, out string? old);
            _undo.Execute(new EditParameterCommand(SelectedNode, row.Name, old, row.Value));

            row.MarkClean();
        }
    }

    // ── SQL Trace extraction ──────────────────────────────────────────────────

    private static (string? context, string? fragment) ExtractTrace(NodeViewModel node, string sql)
    {
        switch (node.Type)
        {
            case NodeType.TableSource:
            {
                string name = node.Title.Trim();
                Match m = Regex.Match(sql,
                    $@"(?:FROM|JOIN)\s+({Regex.Escape(name)}(?:\s+\w+)?)",
                    RegexOptions.IgnoreCase);
                if (m.Success)
                    return ("Source table in FROM / JOIN clause", m.Value.Trim());
                return ("Source table", $"Table: {name}");
            }
            case NodeType.RowSetJoin:
                return ("RowSet join", "rowset LEFT/INNER/RIGHT/FULL JOIN rowset ON condition");
            case NodeType.RowSetFilter:
                return ("RowSet filter", "WHERE condition(s) over input rowset");
            case NodeType.RowSetAggregate:
                return ("RowSet aggregate", "GROUP BY + aggregate metrics over input rowset");
            case NodeType.CteSource:
                return ("CTE source", "FROM cte_name AS alias");
            case NodeType.CteDefinition:
                return ("CTE definition", "WITH cte_name AS (SELECT ... FROM source_table)");
            case NodeType.WhereOutput or NodeType.CompileWhere:
            {
                Match m = Regex.Match(sql,
                    @"WHERE\s+(.+?)(?=\s+(?:GROUP\s+BY|ORDER\s+BY|LIMIT|HAVING|$))",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (m.Success)
                {
                    string cond = m.Groups[1].Value.Trim();
                    if (cond.Length > 80) cond = cond[..77] + "...";
                    return ("Filters applied in WHERE clause", $"WHERE {cond}");
                }
                break;
            }
            case NodeType.Top:
            {
                Match m = Regex.Match(sql, @"LIMIT\s+\d+|TOP\s+\d+", RegexOptions.IgnoreCase);
                if (m.Success)
                    return ("Row count limit", m.Value.Trim());
                break;
            }
            case NodeType.ResultOutput or NodeType.SelectOutput:
            {
                Match m = Regex.Match(sql,
                    @"SELECT\s+(.+?)\s+FROM",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (m.Success)
                {
                    string cols = m.Groups[1].Value.Trim();
                    if (cols.Length > 80) cols = cols[..77] + "...";
                    return ("Final SELECT output columns", $"SELECT {cols}");
                }
                break;
            }
            case NodeType.ColumnList:
            case NodeType.ColumnSetBuilder:
            case NodeType.ColumnSetMerge:
            {
                Match m = Regex.Match(sql,
                    @"SELECT\s+(.+?)\s+FROM",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (m.Success)
                {
                    string cols = m.Groups[1].Value.Trim();
                    if (cols.Length > 80) cols = cols[..77] + "...";
                    return ("Column selection list", $"SELECT {cols}");
                }
                break;
            }
            case NodeType.CountStar:
                return ("Aggregate function", "COUNT(*)");
            case NodeType.ColumnRefCast:
                return ("Column cast", "CAST(column AS type)");
            case NodeType.ScalarFromColumn:
                return ("Scalar extraction", "Use a column reference as scalar expression");
            case NodeType.CountDistinct:
                return ("Aggregate function", "COUNT(DISTINCT ...)");
            case NodeType.Sum:
                return ("Aggregate function", "SUM(...)");
            case NodeType.Avg:
                return ("Aggregate function", "AVG(...)");
            case NodeType.Min:
                return ("Aggregate function", "MIN(...)");
            case NodeType.Max:
                return ("Aggregate function", "MAX(...)");
            case NodeType.StringAgg:
                return ("Aggregate function", "STRING_AGG(..., ', ')");
            case NodeType.WindowFunction:
                return ("Window function", "ROW_NUMBER/RANK/DENSE_RANK/NTILE/LAG/LEAD/FIRST_VALUE/LAST_VALUE OVER (...) ");
            case NodeType.And:
                return ("Logic gate", "... AND ...");
            case NodeType.Or:
                return ("Logic gate", "... OR ...");
            case NodeType.Not:
                return ("Logic gate", "NOT (...)");
            case NodeType.Equals:
                return ("Comparison predicate", "col = value");
            case NodeType.NotEquals:
                return ("Comparison predicate", "col <> value");
            case NodeType.GreaterThan:
                return ("Comparison predicate", "col > value");
            case NodeType.GreaterOrEqual:
                return ("Comparison predicate", "col >= value");
            case NodeType.LessThan:
                return ("Comparison predicate", "col < value");
            case NodeType.LessOrEqual:
                return ("Comparison predicate", "col <= value");
            case NodeType.Between:
                return ("Comparison predicate", "col BETWEEN a AND b");
            case NodeType.NotBetween:
                return ("Comparison predicate", "col NOT BETWEEN a AND b");
            case NodeType.IsNull:
                return ("Comparison predicate", "col IS NULL");
            case NodeType.IsNotNull:
                return ("Comparison predicate", "col IS NOT NULL");
            case NodeType.Like:
                return ("Comparison predicate", "col LIKE pattern");
            case NodeType.NotLike:
                return ("Comparison predicate", "col NOT LIKE pattern");
            case NodeType.Upper:
                return ("String transform", "UPPER(col)");
            case NodeType.Lower:
                return ("String transform", "LOWER(col)");
            case NodeType.Trim:
                return ("String transform", "TRIM(col)");
            case NodeType.Concat:
                return ("String transform", "CONCAT(...)");
            case NodeType.Substring:
                return ("String transform", "SUBSTRING(col, start, length)");
            case NodeType.StringLength:
                return ("String transform", "LENGTH(col)");
            case NodeType.Replace:
                return ("String transform", "REPLACE(col, search, replace)");
            case NodeType.RegexMatch:
                return ("String transform", "col REGEXP pattern");
            case NodeType.RegexExtract or NodeType.RegexReplace:
                return ("String transform", "REGEXP_REPLACE(col, ...)");
            case NodeType.Round:
                return ("Math transform", "ROUND(col, decimals)");
            case NodeType.Abs:
                return ("Math transform", "ABS(col)");
            case NodeType.Ceil:
                return ("Math transform", "CEIL(col)");
            case NodeType.Floor:
                return ("Math transform", "FLOOR(col)");
            case NodeType.Add:
                return ("Math transform", "col + value");
            case NodeType.Subtract:
                return ("Math transform", "col - value");
            case NodeType.Multiply:
                return ("Math transform", "col * value");
            case NodeType.Divide:
                return ("Math transform", "col / value");
            case NodeType.Modulo:
                return ("Math transform", "col % value");
            case NodeType.DateAdd:
                return ("Date arithmetic", "DATEADD(unit, amount, date)");
            case NodeType.DateDiff:
                return ("Date arithmetic", "DATEDIFF(unit, start, end)");
            case NodeType.DatePart:
                return ("Date arithmetic", "DATEPART(unit, date)");
            case NodeType.DateFormat:
                return ("Date arithmetic", "FORMAT(date, pattern)");
            case NodeType.Cast:
                return ("Type cast", "CAST(col AS type)");
            case NodeType.Alias:
            {
                string alias = string.IsNullOrWhiteSpace(node.Alias) ? node.Title : node.Alias;
                return ("Column alias", $"col AS {alias}");
            }
            case NodeType.JsonExtract or NodeType.JsonValue:
                return ("JSON extract", "JSON_EXTRACT(col, path)");
            case NodeType.JsonArrayLength:
                return ("JSON function", "JSON_ARRAY_LENGTH(col)");
            case NodeType.Case:
                return ("Conditional", "CASE WHEN ... THEN ... END");
            case NodeType.NullFill:
                return ("Conditional", "COALESCE(col, fallback)");
            case NodeType.EmptyFill:
                return ("Conditional", "COALESCE(NULLIF(TRIM(col), ''), fallback)");
            case NodeType.ValueMap:
                return ("Conditional", "CASE WHEN col = src THEN dst ELSE col END");
            case NodeType.ValueNumber or NodeType.ValueString
                or NodeType.ValueDateTime or NodeType.ValueBoolean:
                return ("Literal value", $"Value: {node.Title}");
            case NodeType.SystemDate or NodeType.SystemDateTime:
                return ("System date/time", "CURRENT_TIMESTAMP / NOW() / GETDATE()");
            case NodeType.CurrentDate:
                return ("System date", "CURRENT_DATE / CURDATE() / CAST(GETDATE() AS DATE)");
            case NodeType.CurrentTime:
                return ("System time", "CURRENT_TIME / CURTIME() / CAST(GETDATE() AS TIME)");
        }
        return (null, null);
    }
}
