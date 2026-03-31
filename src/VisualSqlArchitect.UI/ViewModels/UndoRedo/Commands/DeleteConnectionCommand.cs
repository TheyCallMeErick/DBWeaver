namespace VisualSqlArchitect.UI.ViewModels.UndoRedo.Commands;

public sealed class DeleteConnectionCommand(ConnectionViewModel connection) : ICanvasCommand
{
    private readonly ConnectionViewModel _connection = connection;
    public string Description => "Delete connection";

    public void Execute(CanvasViewModel canvas)
    {
        var affectedNodes = new HashSet<NodeViewModel>();
        if (_connection.FromPin?.Owner is not null)
            affectedNodes.Add(_connection.FromPin.Owner);
        if (_connection.ToPin?.Owner is not null)
            affectedNodes.Add(_connection.ToPin.Owner);

        canvas.Connections.Remove(_connection);

        if (_connection.FromPin is not null)
            _connection.FromPin.IsConnected = canvas.Connections.Any(c =>
                c.FromPin == _connection.FromPin
            );
        if (_connection.ToPin is not null)
            _connection.ToPin.IsConnected = canvas.Connections.Any(c =>
                c.ToPin == _connection.ToPin
            );

        canvas.ClearNarrowingIfNeeded(affectedNodes);
    }

    public void Undo(CanvasViewModel canvas)
    {
        canvas.Connections.Add(_connection);
        _connection.FromPin.IsConnected = true;
        if (_connection.ToPin is not null)
            _connection.ToPin.IsConnected = true;
    }
}
