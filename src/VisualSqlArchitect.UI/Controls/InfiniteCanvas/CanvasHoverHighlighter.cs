using Avalonia;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

public static class CanvasHoverHighlighter
{
    public static (PinViewModel? Pin, ConnectionViewModel? Wire) ResolveHoverTarget(
        CanvasViewModel vm,
        BezierWireLayer wires,
        PinDragInteraction? pinDrag,
        Point canvasPoint
    )
    {
        PinViewModel? pin = pinDrag?.HitTestPin(canvasPoint, tolerance: 10);
        bool pinCanHighlight = pin is not null && pin.IsConnected;

        ConnectionViewModel? wire = pinCanHighlight
            ? vm.Connections.FirstOrDefault(c => ReferenceEquals(c.FromPin, pin) || ReferenceEquals(c.ToPin, pin))
            : wires.HitTestWire(canvasPoint, tolerance: 8);

        return (pinCanHighlight ? pin : null, wire);
    }

    public static void ApplyHover(
        CanvasViewModel vm,
        PinViewModel? pin,
        ConnectionViewModel? wire
    )
    {
        foreach (PinViewModel p in vm.Nodes.SelectMany(n => n.AllPins))
            p.IsHovered = false;
        foreach (ConnectionViewModel c in vm.Connections)
            c.IsHighlighted = false;

        if (pin is not null)
        {
            pin.IsHovered = true;
            foreach (ConnectionViewModel conn in vm.Connections.Where(c => ReferenceEquals(c.FromPin, pin) || ReferenceEquals(c.ToPin, pin)))
                conn.IsHighlighted = true;
            return;
        }

        if (wire is null)
            return;

        wire.IsHighlighted = true;
        wire.FromPin.IsHovered = true;
        if (wire.ToPin is not null)
            wire.ToPin.IsHovered = true;
    }

    public static void ClearHover(CanvasViewModel vm)
    {
        foreach (PinViewModel p in vm.Nodes.SelectMany(n => n.AllPins))
            p.IsHovered = false;
        foreach (ConnectionViewModel c in vm.Connections)
            c.IsHighlighted = false;
    }
}
