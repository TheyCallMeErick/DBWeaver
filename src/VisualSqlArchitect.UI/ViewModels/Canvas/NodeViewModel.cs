using System.Collections.ObjectModel;
using System.Data;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using Material.Icons;
using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// Represents a Node in the visual query builder canvas.
/// Nodes are the fundamental building blocks that transform, filter, and compose queries.
/// </summary>
public sealed class NodeViewModel : ViewModelBase
{
    private Point _position;
    private bool _isSelected,
        _isHovered,
        _isOrphan;
    private string? _alias;
    private double _width = 220;
    private List<ValidationIssue> _validationIssues = [];

    /// <summary>Unique identifier for this node instance.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>The type of this node (TableSource, Filter, Join, etc.).</summary>
    public NodeType Type { get; }

    /// <summary>The category this node belongs to (DataSource, Transform, etc.).</summary>
    public NodeCategory Category { get; }

    /// <summary>Display name of this node type.</summary>
    public string Title { get; }

    /// <summary>Longer description/subtitle for context.</summary>
    public string Subtitle { get; }

    /// <summary>Dictionary of configurable parameters specific to this node type.</summary>
    public Dictionary<string, string> Parameters { get; } = [];

    /// <summary>Dictionary of inline literal values for pins (used in serialization).</summary>
    public Dictionary<string, string> PinLiterals { get; } = [];

    /// <summary>Collection of input pins (data flows INTO these).</summary>
    public ObservableCollection<PinViewModel> InputPins { get; } = [];

    /// <summary>Collection of output pins (data flows FROM these).</summary>
    public ObservableCollection<PinViewModel> OutputPins { get; } = [];

    /// <summary>Ordered list of output columns for ResultOutput nodes.</summary>
    public ObservableCollection<OutputColumnEntry> OutputColumnOrder { get; } = [];

    /// <summary>Convenience: all pins (input + output).</summary>
    public IEnumerable<PinViewModel> AllPins => InputPins.Concat(OutputPins);

    // ── Type predicates ──────────────────────────────────────────────────────

    /// <summary>True if this node is the final result output.</summary>
    public bool IsResultOutput => Type == NodeType.ResultOutput;

    /// <summary>True if this node is a ColumnList (multiple column selector).</summary>
    public bool IsColumnList => Type == NodeType.ColumnList;

    /// <summary>True if this is a logic gate (AND or OR).</summary>
    public bool IsLogicGate => Type is NodeType.And or NodeType.Or;

    /// <summary>True if this is a value literal node (Number, String, DateTime, Boolean).</summary>
    public bool IsValueNode =>
        Type
            is NodeType.ValueNumber
                or NodeType.ValueString
                or NodeType.ValueDateTime
                or NodeType.ValueBoolean;

    /// <summary>True when the standard left/right pin columns should be shown.</summary>
    public bool ShowStandardPins => !IsValueNode && !IsColumnList && !IsResultOutput;

    public bool IsValueNumber => Type == NodeType.ValueNumber;
    public bool IsValueString => Type == NodeType.ValueString;
    public bool IsValueDateTime => Type == NodeType.ValueDateTime;
    public bool IsValueBoolean => Type == NodeType.ValueBoolean;

    /// <summary>True if the "No inputs" placeholder should be displayed.</summary>
    public bool ShouldShowNoInputsPlaceholder =>
        InputPins.Count == 0 && !IsValueNode && !IsResultOutput && Type == NodeType.TableSource;

    /// <summary>The value of the literal, synchronized from Parameters["value"].</summary>
    public string ValueNodeText
    {
        get => Parameters.TryGetValue("value", out string? v) ? v : "";
        set
        {
            if (IsValueNode)
            {
                Parameters["value"] = value ?? "";
                RaisePropertyChanged(nameof(ValueNodeText));
            }
        }
    }

    // ── Visual properties ────────────────────────────────────────────────────

    /// <summary>Canvas position (top-left) of this node.</summary>
    public Point Position
    {
        get => _position;
        set => Set(ref _position, value);
    }

    /// <summary>True if this node is selected by the user.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            Set(ref _isSelected, value);
            RaisePropertyChanged(nameof(NodeBorderBrush));
            RaisePropertyChanged(nameof(NodeShadow));
        }
    }

    /// <summary>True if the user is hovering over this node.</summary>
    public bool IsHovered
    {
        get => _isHovered;
        set => Set(ref _isHovered, value);
    }

    /// <summary>True when this node does not contribute to the final query output.</summary>
    public bool IsOrphan
    {
        get => _isOrphan;
        set
        {
            Set(ref _isOrphan, value);
            RaisePropertyChanged(nameof(NodeBorderBrush));
            RaisePropertyChanged(nameof(NodeShadow));
            RaisePropertyChanged(nameof(NodeOpacity));
        }
    }

    /// <summary>Optional user-defined alias for this node.</summary>
    public string? Alias
    {
        get => _alias;
        set => Set(ref _alias, value);
    }

    /// <summary>Visual width of the node card.</summary>
    public double Width
    {
        get => _width;
        set => Set(ref _width, value);
    }

    /// <summary>Header color based on node category (for visual distinction).</summary>
    public Color HeaderColor =>
        Category switch
        {
            NodeCategory.DataSource => Color.Parse("#0F766E"),
            NodeCategory.StringTransform => Color.Parse("#4338CA"),
            NodeCategory.MathTransform => Color.Parse("#B45309"),
            NodeCategory.TypeCast => Color.Parse("#7E22CE"),
            NodeCategory.Comparison => Color.Parse("#BE123C"),
            NodeCategory.LogicGate => Color.Parse("#C2410C"),
            NodeCategory.Json => Color.Parse("#6D28D9"),
            NodeCategory.Aggregate => Color.Parse("#15803D"),
            NodeCategory.Conditional => Color.Parse("#0E7490"),
            _ => Color.Parse("#374151"),
        };

    /// <summary>Lighter shade of HeaderColor for gradient.</summary>
    public Color HeaderColorLight =>
        Category switch
        {
            NodeCategory.DataSource => Color.Parse("#14B8A6"),
            NodeCategory.StringTransform => Color.Parse("#818CF8"),
            NodeCategory.MathTransform => Color.Parse("#FBBF24"),
            NodeCategory.TypeCast => Color.Parse("#C084FC"),
            NodeCategory.Comparison => Color.Parse("#FB7185"),
            NodeCategory.LogicGate => Color.Parse("#FB923C"),
            NodeCategory.Json => Color.Parse("#A78BFA"),
            NodeCategory.Aggregate => Color.Parse("#4ADE80"),
            NodeCategory.Conditional => Color.Parse("#22D3EE"),
            _ => Color.Parse("#9CA3AF"),
        };

    /// <summary>Gradient brush for the node header.</summary>
    public LinearGradientBrush HeaderGradient =>
        new()
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(HeaderColor, 0.0),
                new GradientStop(HeaderColorLight, 1.0),
            ],
        };

    // ── Validation state ─────────────────────────────────────────────────────

    /// <summary>List of validation issues (errors, warnings) for this node.</summary>
    public IReadOnlyList<ValidationIssue> ValidationIssues => _validationIssues;

    /// <summary>True if this node has any validation errors.</summary>
    public bool HasError => _validationIssues.Any(i => i.Severity == IssueSeverity.Error);

    /// <summary>True if this node has warnings (but no errors).</summary>
    public bool HasWarning =>
        !HasError && _validationIssues.Any(i => i.Severity == IssueSeverity.Warning);

    /// <summary>Formatted tooltip text showing all validation issues.</summary>
    public string? ValidationTooltip =>
        _validationIssues.Count > 0
            ? string.Join(
                "\n",
                _validationIssues.Select(i =>
                    $"{(i.Severity == IssueSeverity.Error ? "✕" : "⚠")} {i.Message}"
                    + (i.Suggestion is not null ? $"\n   → {i.Suggestion}" : "")
                )
            )
            : null;

    /// <summary>Set validation issues for this node (called by ValidationService).</summary>
    internal void SetValidation(IEnumerable<ValidationIssue> issues)
    {
        _validationIssues = [.. issues];
        RaisePropertyChanged(nameof(ValidationIssues));
        RaisePropertyChanged(nameof(HasError));
        RaisePropertyChanged(nameof(HasWarning));
        RaisePropertyChanged(nameof(ValidationTooltip));
        RaisePropertyChanged(nameof(NodeBorderBrush));
        RaisePropertyChanged(nameof(NodeShadow));
        RaisePropertyChanged(nameof(NodeOpacity));
    }

    // ── ResultOutput column ordering ─────────────────────────────────────────

    /// <summary>
    /// Rebuilds OutputColumnOrder from current canvas connections.
    /// Keeps user-defined order, appends new ones, removes deleted ones.
    /// </summary>
    internal void SyncOutputColumns(IEnumerable<ConnectionViewModel> allConnections)
    {
        if (!IsResultOutput)
            return;

        var incoming = allConnections.Where(c => c.ToPin?.Owner == this).ToList();
        var existingKeys = OutputColumnOrder.Select(e => e.Key).ToHashSet();
        var newKeys = incoming.Select(c => MakeKey(c.FromPin)).ToHashSet();

        // Remove orphaned entries
        foreach (OutputColumnEntry? entry in OutputColumnOrder.ToList())
            if (!newKeys.Contains(entry.Key))
                OutputColumnOrder.Remove(entry);

        // Append newly connected columns
        foreach (ConnectionViewModel? conn in incoming)
        {
            string key = MakeKey(conn.FromPin);
            if (!existingKeys.Contains(key))
            {
                string display =
                    conn.FromPin.Owner.Type == NodeType.TableSource
                        ? $"{conn.FromPin.Owner.Subtitle?.Split('.').Last() ?? conn.FromPin.Owner.Title}.{conn.FromPin.Name}"
                        : $"{conn.FromPin.Owner.Title} → {conn.FromPin.Name}";
                OutputColumnOrder.Add(
                    new OutputColumnEntry(
                        key,
                        display,
                        () => MoveColumnUp(key),
                        () => MoveColumnDown(key)
                    )
                );
            }
        }
    }

    private static string MakeKey(PinViewModel pin) => $"{pin.Owner.Id}::{pin.Name}";

    private void MoveColumnUp(string key)
    {
        int idx = IndexOf(key);
        if (idx > 0)
            OutputColumnOrder.Move(idx, idx - 1);
    }

    private void MoveColumnDown(string key)
    {
        int idx = IndexOf(key);
        if (idx >= 0 && idx < OutputColumnOrder.Count - 1)
            OutputColumnOrder.Move(idx, idx + 1);
    }

    private int IndexOf(string key)
    {
        for (int i = 0; i < OutputColumnOrder.Count; i++)
            if (OutputColumnOrder[i].Key == key)
                return i;
        return -1;
    }

    /// <summary>Returns ordered (nodeId, pinName) pairs for SQL compilation.</summary>
    public IReadOnlyList<(string NodeId, string PinName)> GetOrderedColumns() =>
        OutputColumnOrder
            .Select(e => e.Key.Split("::", 2))
            .Where(p => p.Length == 2)
            .Select(p => (p[0], p[1]))
            .ToList();

    // ── Border/shadow styling based on state ─────────────────────────────────

    /// <summary>Border brush color based on selection/validation state.</summary>
    public SolidColorBrush NodeBorderBrush =>
        IsSelected ? new SolidColorBrush(Color.Parse("#3B82F6"))
        : HasError ? new SolidColorBrush(Color.Parse("#EF4444"))
        : HasWarning ? new SolidColorBrush(Color.Parse("#FBBF24"))
        : IsOrphan ? new SolidColorBrush(Color.Parse("#6B7280"))
        : new SolidColorBrush(Color.Parse("#252C3F"));

    /// <summary>Shadow effects based on state.</summary>
    public BoxShadows NodeShadow =>
        IsSelected ? BoxShadows.Parse("0 0 0 2 #3B82F6, 0 8 32 0 #603B82F6")
        : HasError ? BoxShadows.Parse("0 0 0 2 #EF4444, 0 4 16 0 #40EF4444")
        : HasWarning ? BoxShadows.Parse("0 0 0 1 #FBBF24, 0 4 12 0 #30FBBF24")
        : IsOrphan ? BoxShadows.Parse("0 2 8 0 #206B7280")
        : BoxShadows.Parse("0 4 24 0 #40000000, 0 1 4 0 #50000000");

    /// <summary>Opacity reduced for orphan nodes (visual signal).</summary>
    public double NodeOpacity => IsOrphan ? 0.45 : 1.0;

    /// <summary>Icon resource for this node's category.</summary>
    public string CategoryIcon => NodeIconCatalog.GetForCategory(Category);

    // ── Inline data preview (TableSource nodes only) ──────────────────────────

    private bool _showInlinePreview;
    private bool _isPreviewLoading;
    private DataTable? _inlinePreviewData;
    private string? _inlinePreviewError;

    /// <summary>True if this node supports inline data preview (TableSource only).</summary>
    public bool IsTableSource => Type == NodeType.TableSource;

    /// <summary>Whether the inline preview section is currently expanded.</summary>
    public bool ShowInlinePreview
    {
        get => _showInlinePreview;
        private set => Set(ref _showInlinePreview, value);
    }

    /// <summary>True while a sample query is in flight.</summary>
    public bool IsPreviewLoading
    {
        get => _isPreviewLoading;
        private set
        {
            Set(ref _isPreviewLoading, value);
            RaisePropertyChanged(nameof(HasInlinePreviewData));
            RaisePropertyChanged(nameof(HasInlinePreviewError));
        }
    }

    /// <summary>DataTable populated with up to 5 sample rows from DemoCatalog.</summary>
    public DataTable? InlinePreviewData
    {
        get => _inlinePreviewData;
        private set
        {
            Set(ref _inlinePreviewData, value);
            RaisePropertyChanged(nameof(HasInlinePreviewData));
        }
    }

    /// <summary>Error message when sample query fails.</summary>
    public string? InlinePreviewError
    {
        get => _inlinePreviewError;
        private set
        {
            Set(ref _inlinePreviewError, value);
            RaisePropertyChanged(nameof(HasInlinePreviewError));
        }
    }

    public bool HasInlinePreviewData  => _inlinePreviewData is { Rows.Count: > 0 } && !_isPreviewLoading;
    public bool HasInlinePreviewError => !string.IsNullOrEmpty(_inlinePreviewError) && !_isPreviewLoading;

    /// <summary>Toggle preview panel; auto-loads on first open.</summary>
    public async Task ToggleInlinePreviewAsync()
    {
        ShowInlinePreview = !ShowInlinePreview;
        if (ShowInlinePreview && _inlinePreviewData is null && string.IsNullOrEmpty(_inlinePreviewError))
            await LoadInlinePreviewAsync();
    }

    private async Task LoadInlinePreviewAsync()
    {
        IsPreviewLoading = true;
        InlinePreviewError = null;

        try
        {
            await Task.Delay(450);   // simulate network/DB round-trip

            string tableShort = (Subtitle ?? Title).Split('.').Last().ToLowerInvariant();

            // Resolve schema from DemoCatalog or fall back to first entry
            var entry = InlinePreviewCatalog.FirstOrDefault(e =>
                e.TableName.Contains(tableShort, StringComparison.OrdinalIgnoreCase));

            if (entry is null)
                entry = InlinePreviewCatalog[0];

            var dt   = new DataTable();
            var rng  = new Random((Subtitle ?? Title).GetHashCode() ^ 0xA1B2);

            foreach (var col in entry.Columns)
                dt.Columns.Add(col.Name);

            for (int row = 1; row <= 5; row++)
            {
                var r = dt.NewRow();
                foreach (var col in entry.Columns)
                    r[col.Name] = col.Generator(row, rng);
                dt.Rows.Add(r);
            }

            await Dispatcher.UIThread.InvokeAsync(() => InlinePreviewData = dt);
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => InlinePreviewError = ex.Message);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsPreviewLoading = false);
        }
    }

    // ── Static inline catalog (Northwind sample data) ────────────────────────

    private sealed record ColDef(string Name, Func<int, Random, object> Generator);
    private sealed record TableDef(string TableName, ColDef[] Columns);

    private static readonly string[] _statuses = ["ACTIVE", "SHIPPED", "PENDING", "CANCELLED"];
    private static readonly string[] _cities   = ["New York", "São Paulo", "London", "Berlin", "Tokyo"];
    private static readonly string[] _names    = ["Alice", "Bob", "Carol", "Dave", "Eve", "Frank"];
    private static readonly string[] _emails   = ["alice@ex.com", "bob@ex.com", "carol@ex.com", "dave@ex.com"];
    private static readonly string[] _products = ["Widget A", "Gadget B", "Gizmo C", "Doohickey D"];

    private static readonly TableDef[] InlinePreviewCatalog =
    [
        new("orders",
        [
            new("id",          (row, _)   => (object)row),
            new("customer_id", (row, rng) => rng.Next(1, 20)),
            new("status",      (_,   rng) => _statuses[rng.Next(_statuses.Length)]),
            new("total",       (_,   rng) => Math.Round(rng.NextDouble() * 4000 + 50, 2)),
            new("created_at",  (_,   rng) => DateTime.UtcNow.AddDays(-rng.Next(0, 365)).ToString("yyyy-MM-dd")),
        ]),
        new("customers",
        [
            new("id",         (row, _)   => (object)row),
            new("name",       (_,   rng) => _names[rng.Next(_names.Length)]),
            new("email",      (_,   rng) => _emails[rng.Next(_emails.Length)]),
            new("city",       (_,   rng) => _cities[rng.Next(_cities.Length)]),
            new("created_at", (_,   rng) => DateTime.UtcNow.AddDays(-rng.Next(0, 730)).ToString("yyyy-MM-dd")),
        ]),
        new("products",
        [
            new("id",       (row, _)   => (object)row),
            new("name",     (_,   rng) => _products[rng.Next(_products.Length)]),
            new("price",    (_,   rng) => Math.Round(rng.NextDouble() * 200 + 5, 2)),
            new("stock",    (_,   rng) => rng.Next(0, 500)),
        ]),
        new("default",
        [
            new("id",    (row, _)   => (object)row),
            new("name",  (_,   rng) => _names[rng.Next(_names.Length)]),
            new("value", (_,   rng) => rng.Next(1, 1000)),
        ]),
    ];

    /// <summary>Material icon kind for category visualization.</summary>
    public MaterialIconKind CategoryIconKind => NodeIconCatalog.GetKindForCategory(Category);

    // ── Constructors ─────────────────────────────────────────────────────────

    /// <summary>
    /// Create a new node from a NodeDefinition.
    /// Initializes all standard pins from the definition.
    /// For ColumnList, adds the special "columns" input pin.
    /// For AND/OR gates, adds the initial "cond_1" input pin.
    /// </summary>
    public NodeViewModel(NodeDefinition def, Point pos)
    {
        Type = def.Type;
        Category = def.Category;
        Title = def.DisplayName;
        Subtitle = def.Description.Length > 40 ? def.Description[..37] + "…" : def.Description;
        Position = pos;

        // Initialize parameters with defaults
        foreach (NodeParameter p in def.Parameters)
            if (p.DefaultValue is not null)
                Parameters[p.Name] = p.DefaultValue;

        // Create pins from definition
        foreach (PinDescriptor pin in def.Pins)
        {
            var vm = new PinViewModel(pin, this);
            if (pin.Direction == PinDirection.Input)
                InputPins.Add(vm);
            else
                OutputPins.Add(vm);
        }

        // ColumnList: add the "columns" input pin ready for connection
        if (def.Type == NodeType.ColumnList)
            InputPins.Add(
                new PinViewModel(
                    new PinDescriptor(
                        "columns",
                        PinDirection.Input,
                        PinDataType.Any,
                        IsRequired: false,
                        AllowMultiple: true,
                        Description: "Connect columns or expressions to include in the list"
                    ),
                    this
                )
            );

        // AND/OR gates: add initial "cond_1" input pin
        if (def.Type is NodeType.And or NodeType.Or)
            InputPins.Insert(
                0,
                new PinViewModel(
                    new PinDescriptor(
                        "cond_1",
                        PinDirection.Input,
                        PinDataType.Boolean,
                        IsRequired: false,
                        Description: "Connect a boolean condition"
                    ),
                    this
                )
            );
    }

    /// <summary>
    /// Create a TableSource node from database metadata.
    /// Each column becomes an output pin.
    /// </summary>
    public NodeViewModel(string tableName, IEnumerable<(string n, PinDataType t)> cols, Point pos)
    {
        Type = NodeType.TableSource;
        Category = NodeCategory.DataSource;
        Title = tableName.Split('.').Last();
        Subtitle = tableName;
        Position = pos;
        foreach ((string n, PinDataType t) in cols)
            OutputPins.Add(new PinViewModel(new PinDescriptor(n, PinDirection.Output, t), this));
    }

    // ── Pin management ───────────────────────────────────────────────────────

    /// <summary>Find a pin by name and optional direction.</summary>
    public PinViewModel? FindPin(string name, PinDirection? dir = null) =>
        AllPins.FirstOrDefault(p => p.Name == name && (dir is null || p.Direction == dir));

    /// <summary>Notify when a parameter changes (for binding).</summary>
    public void RaiseParameterChanged(string p) => RaisePropertyChanged($"Param_{p}");

    // ── Dynamic pin synchronization ──────────────────────────────────────────

    /// <summary>
    /// Sync ColumnList pins: ensure "columns" input pin exists.
    /// </summary>
    internal void SyncColumnListPins(IEnumerable<ConnectionViewModel> _)
    {
        if (!IsColumnList)
            return;

        // Ensure the single input pin exists
        if (InputPins.FirstOrDefault(p => p.Name == "columns") == null)
        {
            InputPins.Add(
                new PinViewModel(
                    new PinDescriptor(
                        "columns",
                        PinDirection.Input,
                        PinDataType.Any,
                        IsRequired: false,
                        AllowMultiple: true,
                        Description: "Connect columns or expressions to include in the list"
                    ),
                    this
                )
            );
        }
    }

    /// <summary>
    /// Sync AND/OR gate pins: manage dynamic input pins for conditions.
    /// </summary>
    internal void SyncLogicGatePins(IEnumerable<ConnectionViewModel> c)
    {
        if (IsLogicGate)
            SyncDynamicInputPins("cond_", PinDataType.Boolean, c);
    }

    /// <summary>
    /// Shared implementation for dynamic pin management.
    /// Keeps connected pins, removes disconnected, always leaves one empty slot.
    /// </summary>
    private void SyncDynamicInputPins(
        string prefix,
        PinDataType slotType,
        IEnumerable<ConnectionViewModel> allConnections
    )
    {
        var connectedPinNames = allConnections
            .Where(c => c.ToPin?.Owner == this && (c.ToPin?.Name.StartsWith(prefix) ?? false))
            .Select(c => c.ToPin!.Name)
            .ToHashSet();

        // Remove disconnected pins
        foreach (
            PinViewModel? p in InputPins
                .Where(p => p.Name.StartsWith(prefix) && !connectedPinNames.Contains(p.Name))
                .ToList()
        )
            InputPins.Remove(p);

        // Update connection status
        foreach (PinViewModel? p in InputPins.Where(p => p.Name.StartsWith(prefix)))
            p.IsConnected = connectedPinNames.Contains(p.Name);

        // Find next available slot number
        var used = InputPins
            .Where(p => p.Name.StartsWith(prefix) && int.TryParse(p.Name[prefix.Length..], out _))
            .Select(p => int.Parse(p.Name[prefix.Length..]))
            .ToHashSet();
        int next = 1;
        while (used.Contains(next))
            next++;

        // Add empty slot for next connection
        InputPins.Add(
            new PinViewModel(
                new PinDescriptor(
                    $"{prefix}{next}",
                    PinDirection.Input,
                    slotType,
                    IsRequired: false,
                    Description: slotType == PinDataType.Boolean
                        ? "Connect a boolean condition"
                        : "Connect a column or expression"
                ),
                this
            )
        );
    }
}
