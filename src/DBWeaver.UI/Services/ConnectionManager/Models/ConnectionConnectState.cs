namespace DBWeaver.UI.Services.ConnectionManager;

public readonly record struct ConnectionConnectState(
    bool Started,
    ConnectionProfile? Profile,
    CancellationTokenSource? ConnectCts,
    bool IsConnecting,
    string? ActiveProfileId);

