using System.Collections.ObjectModel;
using Avalonia;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels.UndoRedo.Commands;

namespace VisualSqlArchitect.UI.ViewModels.Canvas;

/// <summary>
/// Manages node lifecycle on the canvas: spawning, deletion, and demo data.
/// Delegates persistence to UndoRedoStack so all operations are undoable.
/// </summary>
public sealed class NodeManager(
    ObservableCollection<NodeViewModel> nodes,
    ObservableCollection<ConnectionViewModel> connections,
    UndoRedoStack undoRedo,
    PropertyPanelViewModel propertyPanel,
    SearchMenuViewModel searchMenu
)
{
    private readonly ObservableCollection<NodeViewModel> _nodes = nodes;
    private readonly ObservableCollection<ConnectionViewModel> _connections = connections;
    private readonly UndoRedoStack _undoRedo = undoRedo;
    private readonly PropertyPanelViewModel _propertyPanel = propertyPanel;
    private readonly SearchMenuViewModel _searchMenu = searchMenu;

    // ── Demo / table catalog ──────────────────────────────────────────────────

    /// <summary>
    /// Demo table catalog (Northwind-style schema) used for testing and the search menu.
    /// </summary>
    public static readonly IReadOnlyList<(
        string FullName,
        IReadOnlyList<(string Name, PinDataType Type)> Cols
    )> DemoCatalog =
    [
        (
            "public.orders",
            new[]
            {
                ("id", PinDataType.Number),
                ("customer_id", PinDataType.Number),
                ("status", PinDataType.Text),
                ("total", PinDataType.Number),
                ("created_at", PinDataType.DateTime),
                ("metadata", PinDataType.Json),
            }
        ),
        (
            "public.customers",
            new[]
            {
                ("id", PinDataType.Number),
                ("name", PinDataType.Text),
                ("email", PinDataType.Text),
                ("city", PinDataType.Text),
                ("country", PinDataType.Text),
                ("created_at", PinDataType.DateTime),
            }
        ),
        (
            "public.products",
            new[]
            {
                ("id", PinDataType.Number),
                ("name", PinDataType.Text),
                ("category", PinDataType.Text),
                ("price", PinDataType.Number),
                ("stock", PinDataType.Number),
            }
        ),
        (
            "public.order_items",
            new[]
            {
                ("id", PinDataType.Number),
                ("order_id", PinDataType.Number),
                ("product_id", PinDataType.Number),
                ("qty", PinDataType.Number),
                ("unit_price", PinDataType.Number),
            }
        ),
        (
            "public.employees",
            new[]
            {
                ("id", PinDataType.Number),
                ("name", PinDataType.Text),
                ("department", PinDataType.Text),
                ("salary", PinDataType.Number),
                ("hire_date", PinDataType.DateTime),
            }
        ),
    ];

    // ── Spawning ──────────────────────────────────────────────────────────────

    /// <summary>Spawns a typed node at the given canvas position (undoable).</summary>
    public NodeViewModel SpawnNode(NodeDefinition def, Point pos)
    {
        var vm = new NodeViewModel(def, pos)
        {
            ZOrder = _nodes.Count == 0 ? 0 : _nodes.Max(n => n.ZOrder) + 1,
        };
        _undoRedo.Execute(new AddNodeCommand(vm));
        _searchMenu.Close();
        return vm;
    }

    /// <summary>Spawns a table/datasource node with explicit column definitions (undoable).</summary>
    public NodeViewModel SpawnTableNode(
        string table,
        IEnumerable<(string n, PinDataType t)> cols,
        Point pos
    )
    {
        var vm = new NodeViewModel(table, cols, pos)
        {
            ZOrder = _nodes.Count == 0 ? 0 : _nodes.Max(n => n.ZOrder) + 1,
        };
        _undoRedo.Execute(new AddNodeCommand(vm));
        _searchMenu.Close();
        return vm;
    }

    // ── Deletion ──────────────────────────────────────────────────────────────

    /// <summary>Deletes all currently selected nodes and their attached connections (undoable).</summary>
    public void DeleteSelected()
    {
        List<NodeViewModel> nodes = [.. _nodes.Where(n => n.IsSelected)];
        if (nodes.Count == 0)
            return;

        var nodeSet = new HashSet<NodeViewModel>(nodes);

        List<ConnectionViewModel> wires =
        [
            .. _connections.Where(c =>
                nodeSet.Contains(c.FromPin.Owner)
                || (c.ToPin is not null && nodeSet.Contains(c.ToPin.Owner))
            ),
        ];

        _undoRedo.Execute(new DeleteSelectionCommand(nodes, wires));
        _propertyPanel.Clear();
    }

    /// <summary>Removes all orphan nodes (and their attached wires) from the canvas (undoable).</summary>
    public void CleanupOrphans()
    {
        List<NodeViewModel> orphans = [.. _nodes.Where(n => n.IsOrphan)];
        if (orphans.Count == 0)
            return;

        var orphanSet = new HashSet<NodeViewModel>(orphans);

        List<ConnectionViewModel> wires =
        [
            .. _connections.Where(c =>
                orphanSet.Contains(c.FromPin.Owner)
                || (c.ToPin is not null && orphanSet.Contains(c.ToPin.Owner))
            ),
        ];

        _undoRedo.Execute(new DeleteOrphansCommand(orphans, wires));
        _propertyPanel.Clear();
    }

    // ── Demo data ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates the canvas with a pre-built demo graph for first-run experience.
    /// Clears undo history and dirty flag after building.
    /// </summary>
    internal void SpawnDemoNodes(UndoRedoStack undoRedo)
    {
        var orders = new NodeViewModel(
            "public.orders",
            [
                ("id", PinDataType.Number),
                ("customer_id", PinDataType.Number),
                ("status", PinDataType.Text),
                ("total", PinDataType.Number),
                ("created_at", PinDataType.DateTime),
                ("metadata", PinDataType.Json),
            ],
            new Point(60, 80)
        );
        _nodes.Add(orders);

        var upper = new NodeViewModel(
            NodeDefinitionRegistry.Get(NodeType.Upper),
            new Point(380, 120)
        )
        {
            Alias = "StatusUpper",
        };
        _nodes.Add(upper);

        var between = new NodeViewModel(
            NodeDefinitionRegistry.Get(NodeType.Between),
            new Point(380, 280)
        );
        between.PinLiterals["low"] = "100";
        between.PinLiterals["high"] = "9999";
        _nodes.Add(between);

        var json = new NodeViewModel(
            NodeDefinitionRegistry.Get(NodeType.JsonExtract),
            new Point(380, 430)
        )
        {
            Alias = "City",
        };
        json.Parameters["path"] = "$.address.city";
        _nodes.Add(json);

        var and = new NodeViewModel(
            NodeDefinitionRegistry.Get(NodeType.And),
            new Point(660, 310)
        );
        _nodes.Add(and);

        _connections.Add(
            new ConnectionViewModel(
                orders.OutputPins.First(p => p.Name == "status"),
                default,
                default
            )
            {
                ToPin = upper.InputPins.First(p => p.Name == "text"),
            }
        );
        _connections.Add(
            new ConnectionViewModel(
                orders.OutputPins.First(p => p.Name == "total"),
                default,
                default
            )
            {
                ToPin = between.InputPins.First(p => p.Name == "value"),
            }
        );
        _connections.Add(
            new ConnectionViewModel(
                orders.OutputPins.First(p => p.Name == "metadata"),
                default,
                default
            )
            {
                ToPin = json.InputPins.First(p => p.Name == "json"),
            }
        );
        _connections.Add(
            new ConnectionViewModel(
                between.OutputPins.First(p => p.Name == "result"),
                default,
                default
            )
            {
                ToPin = and.InputPins.First(p => p.Name == "cond_1"),
            }
        );

        undoRedo.Clear();
    }
}
