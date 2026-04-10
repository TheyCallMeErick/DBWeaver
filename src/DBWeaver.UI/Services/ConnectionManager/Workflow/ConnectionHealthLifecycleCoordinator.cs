using DBWeaver.Core;

namespace DBWeaver.UI.Services.ConnectionManager;

public sealed class ConnectionHealthLifecycleCoordinator(
    IConnectionHealthMonitorService healthMonitorService) : IConnectionHealthLifecycleCoordinator
{
    private readonly IConnectionHealthMonitorService _healthMonitorService = healthMonitorService;

    public CancellationTokenSource? Restart(
        string? activeProfileId,
        CancellationTokenSource? existing,
        Action<CancellationToken> startLoop)
    {
        return _healthMonitorService.Restart(activeProfileId, existing, startLoop);
    }

    public Task<ConnectionHealthStatus> EvaluateActiveStatusAsync(
        IReadOnlyCollection<ConnectionProfile> profiles,
        string? activeProfileId,
        Func<ConnectionConfig, DatabaseProvider, int, CancellationToken, Task<ConnectionTestResult>> runTestAsync,
        double degradedLatencyThresholdMs,
        CancellationToken ct = default)
    {
        ConnectionProfile? profile = profiles.FirstOrDefault(x => x.Id == activeProfileId);
        return _healthMonitorService.EvaluateStatusAsync(
            profile,
            runTestAsync,
            degradedLatencyThresholdMs,
            ct);
    }
}

