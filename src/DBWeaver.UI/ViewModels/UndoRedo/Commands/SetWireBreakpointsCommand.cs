namespace DBWeaver.UI.ViewModels.UndoRedo.Commands;

public sealed class SetWireBreakpointsCommand(
    ConnectionViewModel wire,
    IReadOnlyList<WireBreakpoint> before,
    IReadOnlyList<WireBreakpoint> after,
    string description
) : ICanvasCommand
{
    private readonly ConnectionViewModel _wire = wire;
    private readonly IReadOnlyList<WireBreakpoint> _before = before;
    private readonly IReadOnlyList<WireBreakpoint> _after = after;
    public string Description { get; } = description;

    public void Execute(CanvasViewModel canvas)
    {
        _wire.SetBreakpoints(_after);
        canvas.IsDirty = true;
    }

    public void Undo(CanvasViewModel canvas)
    {
        _wire.SetBreakpoints(_before);
        canvas.IsDirty = true;
    }
}
