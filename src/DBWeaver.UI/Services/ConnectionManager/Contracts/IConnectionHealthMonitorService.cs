using DBWeaver.Core;

namespace DBWeaver.UI.Services.ConnectionManager;

public interface IConnectionHealthMonitorService
{
    CancellationTokenSource? Restart(
        string? activeProfileId,
        CancellationTokenSource? existing,
        Action<CancellationToken> startLoop);

    Task HealthMonitorLoopAsync(CancellationToken ct, Func<CancellationToken, Task> runHealthCheckAsync);

    Task<ConnectionHealthStatus> EvaluateStatusAsync(
        ConnectionProfile? profile,
        Func<ConnectionConfig, DatabaseProvider, int, CancellationToken, Task<ConnectionTestResult>> runTestAsync,
        double degradedLatencyThresholdMs,
        CancellationToken ct = default);
}

