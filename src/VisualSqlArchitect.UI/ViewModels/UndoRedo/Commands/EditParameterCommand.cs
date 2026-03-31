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

    /// <summary>
    /// Coalesces consecutive edits to the same node parameter into one undo entry.
    /// The merged command keeps the original "from" value and uses the latest "to" value.
    /// </summary>
    public ICanvasCommand? TryMerge(ICanvasCommand next)
    {
        if (next is EditParameterCommand other &&
            other._node == _node &&
            other._paramName == _paramName)
        {
            return new EditParameterCommand(_node, _paramName, _oldValue, other._newValue);
        }
        return null;
    }

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
