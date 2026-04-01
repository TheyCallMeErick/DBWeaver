using System.Collections.ObjectModel;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels.UndoRedo.Commands;

namespace VisualSqlArchitect.UI.ViewModels.Canvas;

/// <summary>
/// Manages pin connections on the canvas.
/// Handles wire creation and deletion.
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
    /// Creates a new connection between two pins.
    /// </summary>
    public void ConnectPins(PinViewModel from, PinViewModel to)
    {
        PinViewModel src = from.Direction == PinDirection.Output ? from : to;
        PinViewModel dest = from.Direction == PinDirection.Input ? from : to;

        if (!dest.CanAccept(src))
            return;

        ConnectionViewModel? displaced = dest.AllowMultiple
            ? null
            : _connections.FirstOrDefault(c => c.ToPin == dest);
        var conn = new ConnectionViewModel(src, src.AbsolutePosition, dest.AbsolutePosition)
        {
            ToPin = dest,
        };
        _undoRedo.Execute(new AddConnectionCommand(conn, displaced));

        ApplyComparisonConcretization(src, dest);
    }

    /// <summary>
    /// Deletes an existing connection from the canvas.
    /// </summary>
    public void DeleteConnection(ConnectionViewModel conn) =>
        _undoRedo.Execute(new DeleteConnectionCommand(conn));

    /// <summary>
    /// Legacy method kept for compatibility while narrowing is phased out.
    /// </summary>
    private static void NarrowPinTypes(PinViewModel srcPin, PinViewModel destPin)
    {
        _ = srcPin;
        _ = destPin;
    }

    /// <summary>
    /// Legacy method kept for compatibility while narrowing is phased out.
    /// </summary>
    public void ClearNarrowingIfNeeded(IEnumerable<NodeViewModel> nodes)
    {
        RecomputeComparisonConcretization(nodes);
    }

    private void ApplyComparisonConcretization(PinViewModel src, PinViewModel dest)
    {
        if (dest.Direction != PinDirection.Input)
            return;

        if (!IsComparisonNode(dest.Owner.Type) || dest.EffectiveDataType != PinDataType.ColumnRef)
            return;

        PinDataType? scalarType = ResolveSourceScalarType(src);
        if (scalarType is null)
            return;

        foreach (PinViewModel input in dest.Owner.InputPins)
        {
            if (input.DataType == PinDataType.ColumnRef && input.ColumnRefMeta is null)
                input.ExpectedColumnScalarType = scalarType;
        }
    }

    private void RecomputeComparisonConcretization(IEnumerable<NodeViewModel> nodes)
    {
        foreach (NodeViewModel node in nodes)
        {
            if (!IsComparisonNode(node.Type))
                continue;

            foreach (PinViewModel input in node.InputPins)
            {
                if (input.DataType == PinDataType.ColumnRef && input.ColumnRefMeta is null)
                    input.ExpectedColumnScalarType = null;
            }

            PinDataType? concrete = _connections
                .Where(c => c.ToPin?.Owner == node && c.ToPin.Direction == PinDirection.Input)
                .Select(c => ResolveSourceScalarType(c.FromPin))
                .FirstOrDefault(t => t is not null);

            if (concrete is null)
                continue;

            foreach (PinViewModel input in node.InputPins)
            {
                if (input.DataType == PinDataType.ColumnRef && input.ColumnRefMeta is null)
                    input.ExpectedColumnScalarType = concrete;
            }
        }
    }

    private static PinDataType? ResolveSourceScalarType(PinViewModel source)
    {
        if (source.EffectiveDataType == PinDataType.ColumnRef)
            return source.ColumnRefMeta?.ScalarType ?? source.ExpectedColumnScalarType;

        if (source.EffectiveDataType.IsScalar())
            return source.EffectiveDataType;

        return null;
    }

    private static bool IsComparisonNode(NodeType type) =>
        type is NodeType.Equals
            or NodeType.NotEquals
            or NodeType.GreaterThan
            or NodeType.GreaterOrEqual
            or NodeType.LessThan
            or NodeType.LessOrEqual
            or NodeType.Between
            or NodeType.NotBetween
            or NodeType.IsNull
            or NodeType.IsNotNull;

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
        if (!map.TryGetValue(pin, out HashSet<PinDataType>? set))
        {
            set = [];
            map[pin] = set;
        }

        set.Add(otherType);
    }
}
