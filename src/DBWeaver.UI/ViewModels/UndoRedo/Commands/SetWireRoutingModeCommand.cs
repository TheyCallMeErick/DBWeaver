namespace DBWeaver.UI.ViewModels.UndoRedo.Commands;

public sealed class SetWireRoutingModeCommand(
    ConnectionViewModel wire,
    CanvasWireRoutingMode before,
    CanvasWireRoutingMode after,
    string description
) : ICanvasCommand
{
    private readonly ConnectionViewModel _wire = wire;
    private readonly CanvasWireRoutingMode _before = before;
    private readonly CanvasWireRoutingMode _after = after;
    public string Description { get; } = description;

    public void Execute(CanvasViewModel canvas)
    {
        _wire.RoutingMode = _after;
        canvas.IsDirty = true;
    }

    public void Undo(CanvasViewModel canvas)
    {
        _wire.RoutingMode = _before;
        canvas.IsDirty = true;
    }
}

