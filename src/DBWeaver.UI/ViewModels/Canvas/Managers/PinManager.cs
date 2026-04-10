using System.Collections.ObjectModel;
using DBWeaver.Nodes;
using DBWeaver.Nodes.Pins;
using DBWeaver.UI.ViewModels.UndoRedo.Commands;

namespace DBWeaver.UI.ViewModels.Canvas;

/// <summary>
/// Manages pin connections on the canvas.
/// Handles wire creation and deletion.
/// </summary>
public sealed class PinManager(
    ObservableCollection<NodeViewModel> nodes,
    ObservableCollection<ConnectionViewModel> connections,
    UndoRedoStack undoRedo,
    Func<CanvasWireRoutingMode>? routingModeResolver = null
) : IPinManager
{
    private readonly ObservableCollection<NodeViewModel> _nodes = nodes;
    private readonly ObservableCollection<ConnectionViewModel> _connections = connections;
    private readonly UndoRedoStack _undoRedo = undoRedo;
    private readonly Func<CanvasWireRoutingMode> _routingModeResolver =
        routingModeResolver ?? (() => CanvasWireRoutingMode.Bezier);

    /// <summary>
    /// Creates a new connection between two pins.
    /// </summary>
    public void ConnectPins(PinViewModel from, PinViewModel to)
    {
        PinViewModel src = from.Direction == PinDirection.Output ? from : to;
        PinViewModel dest = from.Direction == PinDirection.Input ? from : to;

        PinConnectionDecision decision = PinDomainAdapter.CanConnect(
            dest,
            src,
            _connections,
            allowImplicitReplacement: true);
        if (!decision.IsAllowed)
            return;

        UndoRedoStack.UndoRedoTransaction? transaction = null;
        bool hasCompositeMutations = decision.Mutations.Count > 0;
        if (hasCompositeMutations && !_undoRedo.InTransaction)
            transaction = _undoRedo.BeginTransaction("Connect pins (domain mutations)");

        try
        {
            ConnectionViewModel? displacedByDomain = ResolveDisplacedConnection(decision);
            ConnectionViewModel created = AddConnection(src, dest, displacedByDomain);
            _ = created;
            ApplyDomainMutations(decision);
            transaction?.Commit();
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    private ConnectionViewModel AddConnection(
        PinViewModel src,
        PinViewModel dest,
        ConnectionViewModel? displacedByDomain)
    {
        ConnectionViewModel? displaced = displacedByDomain ?? (dest.AllowMultiple
            ? null
            : _connections.FirstOrDefault(c => c.ToPin == dest));
        var conn = new ConnectionViewModel(src, src.AbsolutePosition, dest.AbsolutePosition)
        {
            ToPin = dest,
            RoutingMode = _routingModeResolver(),
        };
        _undoRedo.Execute(new AddConnectionCommand(conn, displaced));
        return conn;
    }

    /// <summary>
    /// Deletes an existing connection from the canvas.
    /// </summary>
    public void DeleteConnection(ConnectionViewModel conn)
    {
        UndoRedoStack.UndoRedoTransaction? transaction = null;
        if (!_undoRedo.InTransaction)
            transaction = _undoRedo.BeginTransaction("Delete connection (domain mutations)");

        try
        {
            PinViewModel sourcePin = conn.FromPin;
            PinViewModel? destinationPin = conn.ToPin;

            _undoRedo.Execute(new DeleteConnectionCommand(conn));

            if (destinationPin is not null)
            {
                PinModel sourceModel = PinDomainAdapter.ToPinModel(sourcePin);
                PinModel destinationModel = PinDomainAdapter.ToPinModel(destinationPin);
                PinConnectionContext context = PinDomainAdapter.BuildContextFromConnections(
                    _connections,
                    allowImplicitReplacement: false);
                IReadOnlyList<IPinMutation> mutations = PinDisconnectionEvaluator.EvaluateAfterDisconnect(
                    sourceModel,
                    destinationModel,
                    context);
                ApplyDomainMutations(mutations);
            }

            transaction?.Commit();
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    private ConnectionViewModel? ResolveDisplacedConnection(PinConnectionDecision decision)
    {
        ReplaceExistingConnectionMutation? mutation = decision.Mutations
            .OfType<ReplaceExistingConnectionMutation>()
            .FirstOrDefault();
        if (mutation is null)
            return null;

        return _connections.FirstOrDefault(c => mutation.ReplacedConnectionIds.Contains(c.Id));
    }

    private void ApplyDomainMutations(PinConnectionDecision decision)
        => ApplyDomainMutations(decision.Mutations);

    private void ApplyDomainMutations(IReadOnlyList<IPinMutation> mutations)
    {
        foreach (PruneConnectionsMutation mutation in mutations.OfType<PruneConnectionsMutation>())
            ApplyPruneMutation(mutation);

        foreach (ConcretizeComparisonScalarMutation mutation in mutations.OfType<ConcretizeComparisonScalarMutation>())
            ApplyConcretizeMutation(mutation);

        foreach (ClearComparisonScalarMutation mutation in mutations.OfType<ClearComparisonScalarMutation>())
            ApplyClearComparisonMutation(mutation);
    }

    private void ApplyConcretizeMutation(ConcretizeComparisonScalarMutation mutation)
    {
        _undoRedo.Execute(new SetComparisonConcretizationCommand(mutation.NodeId, mutation.ScalarType));
    }

    private void ApplyClearComparisonMutation(ClearComparisonScalarMutation mutation)
    {
        _undoRedo.Execute(new SetComparisonConcretizationCommand(mutation.NodeId, null));
    }

    private void ApplyPruneMutation(PruneConnectionsMutation mutation)
    {
        foreach (string connectionId in mutation.PrunedConnectionIds)
        {
            ConnectionViewModel? connection = _connections.FirstOrDefault(c => c.Id == connectionId);
            if (connection is not null)
                _undoRedo.Execute(new DeleteConnectionCommand(connection));
        }
    }

}
