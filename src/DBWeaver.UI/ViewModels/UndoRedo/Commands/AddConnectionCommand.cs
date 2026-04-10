namespace DBWeaver.UI.ViewModels.UndoRedo.Commands;

public sealed class AddConnectionCommand(
    ConnectionViewModel connection,
    ConnectionViewModel? displaced = null
) : ICanvasCommand
{
    private readonly ConnectionViewModel _connection = connection;
    private readonly ConnectionViewModel? _displaced = displaced;
    public string Description => $"Connect {_connection.FromPin.Name} → {_connection.ToPin?.Name}";

    public void Execute(CanvasViewModel canvas)
    {
        if (_displaced is not null && canvas.Connections.Contains(_displaced))
            canvas.Connections.Remove(_displaced);

        if (!canvas.Connections.Contains(_connection))
            canvas.Connections.Add(_connection);

        _connection.FromPin.IsConnected = true;
        if (_connection.ToPin is not null)
            _connection.ToPin.IsConnected = true;
    }

    public void Undo(CanvasViewModel canvas)
    {
        canvas.Connections.Remove(_connection);
        _connection.FromPin.IsConnected = canvas.Connections.Any(c =>
            c.FromPin == _connection.FromPin
        );

        if (_connection.ToPin is not null)
        {
            _connection.ToPin.IsConnected = canvas.Connections.Any(c =>
                c.ToPin == _connection.ToPin
            );
        }

        if (_displaced is not null)
            canvas.Connections.Add(_displaced);
    }
}
