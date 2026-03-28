namespace VisualSqlArchitect.UI.ViewModels.UndoRedo.Commands;

public sealed class EditParameterCommand(
    NodeViewModel node,
    string paramName,
    string? oldValue,
    string? newValue
) : ICanvasCommand
{
    private readonly NodeViewModel _node = node;
    private readonly string _paramName = paramName;
    private readonly string? _oldValue = oldValue;
    private readonly string? _newValue = newValue;
    public string Description => $"Edit {_node.Title}.{_paramName}";

    public void Execute(CanvasViewModel canvas)
    {
        if (_newValue is null)
            _node.Parameters.Remove(_paramName);
        else
            _node.Parameters[_paramName] = _newValue;
        _node.RaiseParameterChanged(_paramName);
    }

    public void Undo(CanvasViewModel canvas)
    {
        if (_oldValue is null)
            _node.Parameters.Remove(_paramName);
        else
            _node.Parameters[_paramName] = _oldValue;
        _node.RaiseParameterChanged(_paramName);
    }
}
