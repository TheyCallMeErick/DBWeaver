using System.Collections.ObjectModel;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels.UndoRedo.Commands;

namespace VisualSqlArchitect.UI.ViewModels.Canvas;

/// <summary>
/// Manages pin connections and data type narrowing logic on the canvas.
/// Handles wire creation, deletion, and automatic type narrowing for polymorphic pins.
/// </summary>
public sealed class PinManager(
    ObservableCollection<NodeViewModel> nodes,
    ObservableCollection<ConnectionViewModel> connections,
    UndoRedoStack undoRedo
)
{
    private readonly ObservableCollection<NodeViewModel> _nodes = nodes;
    private readonly ObservableCollection<ConnectionViewModel> _connections = connections;
    private readonly UndoRedoStack _undoRedo = undoRedo;

    /// <summary>
    /// Creates a new connection between two pins, with automatic type narrowing for "Any" type pins.
    /// </summary>
    public void ConnectPins(PinViewModel from, PinViewModel to)
    {
        PinViewModel src = from.Direction == PinDirection.Output ? from : to;
        PinViewModel dest = from.Direction == PinDirection.Input ? from : to;
        ConnectionViewModel? displaced = dest.AllowMultiple
            ? null
            : _connections.FirstOrDefault(c => c.ToPin == dest);
        var conn = new ConnectionViewModel(src, src.AbsolutePosition, dest.AbsolutePosition)
        {
            ToPin = dest,
        };
        _undoRedo.Execute(new AddConnectionCommand(conn, displaced));

        // Apply type narrowing logic for "Any" type pins
        NarrowPinTypes(src, dest);
    }

    /// <summary>
    /// Deletes an existing connection from the canvas.
    /// </summary>
    public void DeleteConnection(ConnectionViewModel conn) =>
        _undoRedo.Execute(new DeleteConnectionCommand(conn));

    /// <summary>
    /// When a connection is made between two pins, if one pin has "Any" data type and the other has a concrete type,
    /// this method narrows all sibling pins in the same node (pins on the same node that also have "Any" type)
    /// to match that concrete type.
    /// </summary>
    private static void NarrowPinTypes(PinViewModel srcPin, PinViewModel destPin)
    {
        PinDataType? concreteType = null;
        NodeViewModel? nodeToNarrow = null;

        // Determine if we should narrow and which node should be narrowed
        if (srcPin.DataType != PinDataType.Any && destPin.DataType == PinDataType.Any)
        {
            concreteType = srcPin.DataType;
            nodeToNarrow = destPin.Owner;
        }
        else if (destPin.DataType != PinDataType.Any && srcPin.DataType == PinDataType.Any)
        {
            concreteType = destPin.DataType;
            nodeToNarrow = srcPin.Owner;
        }

        // If neither condition is true, no narrowing needed
        if (concreteType is null || nodeToNarrow is null)
            return;

        // ColumnList pins never narrow their type — they always remain "Any" to accept all input types
        if (nodeToNarrow.IsColumnList)
            return;

        // For other nodes (AND, OR, etc.): narrow all sibling Any-typed pins uniformly.
        foreach (PinViewModel? pin in nodeToNarrow.InputPins.Concat(nodeToNarrow.OutputPins))
        {
            if (pin.DataType == PinDataType.Any)
                pin.NarrowedDataType = concreteType;
        }
    }

    /// <summary>
    /// Clears narrowed data types on pins if they're no longer justified by existing connections.
    /// A narrowing should be kept only if there's at least one connection where the pin is connected
    /// to another pin with a concrete (non-Any) type that matches the NarrowedDataType.
    /// </summary>
    public void ClearNarrowingIfNeeded(IEnumerable<NodeViewModel> nodes)
    {
        var concreteByPin = BuildConcreteTypesByPin();

        foreach (NodeViewModel node in nodes)
        {
            // Check all pins (both input and output)
            foreach (PinViewModel? pin in node.InputPins.Concat(node.OutputPins))
            {
                // Only process pins that have a narrowed type set
                if (pin.NarrowedDataType is null)
                    continue;

                PinDataType narrowedType = pin.NarrowedDataType.Value;
                bool hasValidConnection =
                    concreteByPin.TryGetValue(pin, out HashSet<PinDataType>? supported)
                    && supported.Contains(narrowedType);

                // If no valid connection exists, reset the narrowing
                if (!hasValidConnection)
                {
                    pin.NarrowedDataType = null;
                }
            }
        }
    }

    private Dictionary<PinViewModel, HashSet<PinDataType>> BuildConcreteTypesByPin()
    {
        var map = new Dictionary<PinViewModel, HashSet<PinDataType>>();

        foreach (ConnectionViewModel conn in _connections)
        {
            if (conn.ToPin is null)
                continue;

            AddConcreteType(map, conn.FromPin, conn.ToPin.DataType);
            AddConcreteType(map, conn.ToPin, conn.FromPin.DataType);
        }

        return map;
    }

    private static void AddConcreteType(
        Dictionary<PinViewModel, HashSet<PinDataType>> map,
        PinViewModel pin,
        PinDataType otherType)
    {
        if (otherType == PinDataType.Any)
            return;

        if (!map.TryGetValue(pin, out HashSet<PinDataType>? set))
        {
            set = [];
            map[pin] = set;
        }

        set.Add(otherType);
    }
}
