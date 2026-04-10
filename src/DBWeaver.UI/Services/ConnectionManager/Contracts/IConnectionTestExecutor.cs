using DBWeaver.Core;

namespace DBWeaver.UI.Services.ConnectionManager;

public interface IConnectionTestExecutor
{
    Task<ConnectionTestResult> ExecuteAsync(
        ConnectionConfig config,
        DatabaseProvider provider,
        int timeoutSeconds,
        CancellationToken ct = default);
}

