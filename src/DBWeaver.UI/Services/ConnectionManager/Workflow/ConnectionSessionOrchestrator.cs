namespace DBWeaver.UI.Services.ConnectionManager;

public sealed class ConnectionSessionOrchestrator : IConnectionSessionOrchestrator
{
    public ConnectionConnectState BeginConnect(ConnectionProfile? selectedProfile, CancellationTokenSource? existingConnectCts)
    {
        if (selectedProfile is null)
            return new ConnectionConnectState(
                Started: false,
                Profile: null,
                ConnectCts: existingConnectCts,
                IsConnecting: false,
                ActiveProfileId: null);

        existingConnectCts?.Cancel();
        existingConnectCts?.Dispose();

        var newCts = new CancellationTokenSource();
        return new ConnectionConnectState(
            Started: true,
            Profile: selectedProfile,
            ConnectCts: newCts,
            IsConnecting: true,
            ActiveProfileId: selectedProfile.Id);
    }

    public ConnectionDisconnectState BeginDisconnect(CancellationTokenSource? existingConnectCts)
    {
        existingConnectCts?.Cancel();
        existingConnectCts?.Dispose();

        return new ConnectionDisconnectState(
            ConnectCts: null,
            IsConnecting: false,
            ActiveProfileId: null);
    }
}

