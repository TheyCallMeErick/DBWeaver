using Avalonia;
using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// Represents the state of an ongoing pin drag operation.
/// Manages the temporary "live wire" and valid drop targets.
/// </summary>
public sealed class PinDragState
{
    /// <summary>The source pin being dragged from (Output pin).</summary>
    public PinViewModel SourcePin { get; }

    /// <summary>
    /// The temporary connection (wire) being shown during drag.
    /// Points from SourcePin to the current mouse position.
    /// </summary>
    public ConnectionViewModel LiveWire { get; }

    /// <summary>List of all pins that can accept this connection.</summary>
    public List<PinViewModel> ValidTargets { get; }

    private readonly List<PinViewModel> _incompatibleTargets;

    // Tracks the single pin currently shown as the active drop target so that
    // SetNearestDropTarget can toggle only the two pins that changed (O(1))
    // instead of iterating all valid targets every frame (O(n)).
    private PinViewModel? _activeDropTarget;

    /// <summary>
    /// Initialize a drag state with a source pin and all its valid drop targets.
    /// Highlights the valid targets visually.
    /// </summary>
    public PinDragState(
        PinViewModel source,
        ConnectionViewModel wire,
        IEnumerable<PinViewModel> allPins
    )
    {
        SourcePin = source;
        LiveWire = wire;
        ValidTargets = [.. allPins.Where(p => p.CanAccept(source))];
        _incompatibleTargets =
        [
            .. allPins.Where(p =>
                p.Owner != source.Owner
                && p.Direction != source.Direction
                && !ValidTargets.Contains(p)
            ),
        ];

        // Highlight all valid targets so the user can see where they can drop.
        // BeginDrag calls UpdateDrag immediately after constructing PinDragState,
        // which transitions to single-target tracking via SetNearestDropTarget.
        foreach (PinViewModel p in ValidTargets)
            p.IsDropTarget = true;

        foreach (PinViewModel p in _incompatibleTargets)
            p.IsDragIncompatible = true;
    }

    /// <summary>Update the end point of the live wire to the current mouse position.</summary>
    public void UpdateWireEnd(Point pt) => LiveWire.ToPoint = pt;

    /// <summary>
    /// Updates the active drop-target highlight in O(1) by toggling only the
    /// pins that actually changed, rather than iterating all valid targets each frame.
    /// </summary>
    public void SetNearestDropTarget(PinViewModel? nearest)
    {
        if (ReferenceEquals(_activeDropTarget, nearest))
            return; // no change — skip all property writes

        if (_activeDropTarget is null)
        {
            // Transition from "all highlighted" (constructor state) to single-target tracking:
            // clear every valid target except the incoming nearest.
            foreach (PinViewModel p in ValidTargets)
                p.IsDropTarget = ReferenceEquals(p, nearest);
        }
        else
        {
            // Steady-state: only toggle the two pins that changed.
            _activeDropTarget.IsDropTarget = false;
            if (nearest is not null)
                nearest.IsDropTarget = true;
        }

        _activeDropTarget = nearest;
    }

    /// <summary>Find the closest valid target within tolerance distance.</summary>
    public PinViewModel? HitTest(Point pt, double tol = 12)
    {
        double tolSq = tol * tol;
        PinViewModel? best = null;
        double bestDist = double.MaxValue;
        foreach (PinViewModel p in ValidTargets)
        {
            double dx = p.AbsolutePosition.X - pt.X;
            double dy = p.AbsolutePosition.Y - pt.Y;
            double dSq = dx * dx + dy * dy;
            if (dSq <= tolSq && dSq < bestDist)
            {
                bestDist = dSq;
                best = p;
            }
        }
        return best;
    }

    /// <summary>Clear the drag state (unhighlight all targets).</summary>
    public void Cancel()
    {
        foreach (PinViewModel p in ValidTargets)
            p.IsDropTarget = false;

        foreach (PinViewModel p in _incompatibleTargets)
            p.IsDragIncompatible = false;

        _activeDropTarget = null;
    }
}
