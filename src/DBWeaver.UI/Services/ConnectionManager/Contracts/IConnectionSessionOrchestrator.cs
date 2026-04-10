namespace DBWeaver.UI.Services.ConnectionManager;

public interface IConnectionSessionOrchestrator
{
    ConnectionConnectState BeginConnect(ConnectionProfile? selectedProfile, CancellationTokenSource? existingConnectCts);

    ConnectionDisconnectState BeginDisconnect(CancellationTokenSource? existingConnectCts);
}

