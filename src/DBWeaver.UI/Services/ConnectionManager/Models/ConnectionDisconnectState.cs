namespace DBWeaver.UI.Services.ConnectionManager;

public readonly record struct ConnectionDisconnectState(
    CancellationTokenSource? ConnectCts,
    bool IsConnecting,
    string? ActiveProfileId);

