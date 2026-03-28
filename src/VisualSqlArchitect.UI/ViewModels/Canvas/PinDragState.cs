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

        // Highlight valid targets so user knows where they can drop
        foreach (PinViewModel p in ValidTargets)
            p.IsDropTarget = true;
    }

    /// <summary>Update the end point of the live wire to the current mouse position.</summary>
    public void UpdateWireEnd(Point pt) => LiveWire.ToPoint = pt;

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

    /// <summary>Clear the drag state (unhighlight targets).</summary>
    public void Cancel()
    {
        foreach (PinViewModel p in ValidTargets)
            p.IsDropTarget = false;
    }
}
