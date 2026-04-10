using DBWeaver.Core;

namespace DBWeaver.UI.Services.ConnectionManager;

public interface IConnectionHealthLifecycleCoordinator
{
    CancellationTokenSource? Restart(
        string? activeProfileId,
        CancellationTokenSource? existing,
        Action<CancellationToken> startLoop);

    Task<ConnectionHealthStatus> EvaluateActiveStatusAsync(
        IReadOnlyCollection<ConnectionProfile> profiles,
        string? activeProfileId,
        Func<ConnectionConfig, DatabaseProvider, int, CancellationToken, Task<ConnectionTestResult>> runTestAsync,
        double degradedLatencyThresholdMs,
        CancellationToken ct = default);
}

