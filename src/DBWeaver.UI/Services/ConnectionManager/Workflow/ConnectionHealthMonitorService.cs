using DBWeaver.Core;

namespace DBWeaver.UI.Services.ConnectionManager;

public sealed class ConnectionHealthMonitorService : IConnectionHealthMonitorService
{
    public CancellationTokenSource? Restart(
        string? activeProfileId,
        CancellationTokenSource? existing,
        Action<CancellationToken> startLoop)
    {
        existing?.Cancel();
        existing?.Dispose();

        if (activeProfileId is null)
            return null;

        var cts = new CancellationTokenSource();
        startLoop(cts.Token);
        return cts;
    }

    public async Task HealthMonitorLoopAsync(CancellationToken ct, Func<CancellationToken, Task> runHealthCheckAsync)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(AppConstants.HealthCheckIntervalSeconds), ct);
                if (!ct.IsCancellationRequested)
                    await runHealthCheckAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown path
        }
    }

    public async Task<ConnectionHealthStatus> EvaluateStatusAsync(
        ConnectionProfile? profile,
        Func<ConnectionConfig, DatabaseProvider, int, CancellationToken, Task<ConnectionTestResult>> runTestAsync,
        double degradedLatencyThresholdMs,
        CancellationToken ct = default)
    {
        if (profile is null)
            return ConnectionHealthStatus.Unknown;

        try
        {
            ConnectionTestResult result = await runTestAsync(
                profile.ToConnectionConfig(),
                profile.Provider,
                profile.TimeoutSeconds,
                ct);

            if (!result.Success)
                return ConnectionHealthStatus.Offline;

            double ms = result.Latency?.TotalMilliseconds ?? 0;
            return ms >= degradedLatencyThresholdMs
                ? ConnectionHealthStatus.Degraded
                : ConnectionHealthStatus.Online;
        }
        catch (OperationCanceledException)
        {
            return ct.IsCancellationRequested
                ? ConnectionHealthStatus.Unknown
                : ConnectionHealthStatus.Offline;
        }
        catch
        {
            return ConnectionHealthStatus.Offline;
        }
    }
}

