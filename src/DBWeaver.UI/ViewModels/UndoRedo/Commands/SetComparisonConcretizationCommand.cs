using DBWeaver.Nodes;

namespace DBWeaver.UI.ViewModels.UndoRedo.Commands;

public sealed class SetComparisonConcretizationCommand(
    string nodeId,
    PinDataType? newScalarType) : ICanvasCommand
{
    private readonly string _nodeId = nodeId;
    private readonly PinDataType? _newScalarType = newScalarType;
    private PinDataType? _previousValue;

    public string Description =>
        _newScalarType is null
            ? "Clear comparison concretization"
            : $"Concretize comparison to {_newScalarType}";

    public void Execute(CanvasViewModel canvas)
    {
        NodeViewModel? node = canvas.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node is null)
            return;

        _previousValue = node.ComparisonExpectedScalarType;
        node.ComparisonExpectedScalarType = _newScalarType;
    }

    public void Undo(CanvasViewModel canvas)
    {
        NodeViewModel? node = canvas.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node is null)
            return;

        node.ComparisonExpectedScalarType = _previousValue;
    }
}
