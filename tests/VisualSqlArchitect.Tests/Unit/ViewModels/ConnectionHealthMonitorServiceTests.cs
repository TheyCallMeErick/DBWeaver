using DBWeaver.UI.Services.ConnectionManager;
using DBWeaver.UI.Services.Benchmark;
using DBWeaver.Core;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class ConnectionHealthMonitorServiceTests
{
    [Fact]
    public void Restart_WhenActiveProfileMissing_ReturnsNullAndDisposesExisting()
    {
        var service = new ConnectionHealthMonitorService();
        using var existing = new CancellationTokenSource();

        CancellationTokenSource? restarted = service.Restart(null, existing, _ => { });

        Assert.Null(restarted);
        Assert.True(existing.IsCancellationRequested);
    }

    [Fact]
    public void Restart_WhenActiveProfilePresent_CreatesNewTokenAndStartsLoop()
    {
        var service = new ConnectionHealthMonitorService();
        bool started = false;

        CancellationTokenSource? restarted = service.Restart("p1", existing: null, _ => started = true);

        Assert.NotNull(restarted);
        Assert.True(started);

        restarted!.Dispose();
    }

    [Fact]
    public async Task EvaluateStatusAsync_WithoutProfile_ReturnsUnknown()
    {
        var service = new ConnectionHealthMonitorService();

        EConnectionHealthStatus status = await service.EvaluateStatusAsync(
            profile: null,
            runTestAsync: (_, _, _, _) => Task.FromResult(new ConnectionTestResult(true, null, null)),
            degradedLatencyThresholdMs: 500);

        Assert.Equal(EConnectionHealthStatus.Unknown, status);
    }

    [Fact]
    public async Task EvaluateStatusAsync_SuccessWithHighLatency_ReturnsDegraded()
    {
        var service = new ConnectionHealthMonitorService();
        var profile = new ConnectionProfile
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Local",
            Provider = DatabaseProvider.Postgres,
            Host = "localhost",
            Port = 5432,
            Database = "db",
            Username = "u",
            Password = "p",
            TimeoutSeconds = 30,
        };

        EConnectionHealthStatus status = await service.EvaluateStatusAsync(
            profile,
            runTestAsync: (_, _, _, _) => Task.FromResult(new ConnectionTestResult(true, null, TimeSpan.FromMilliseconds(800))),
            degradedLatencyThresholdMs: 500);

        Assert.Equal(EConnectionHealthStatus.Degraded, status);
    }

    [Fact]
    public async Task EvaluateStatusAsync_FailedTest_ReturnsOffline()
    {
        var service = new ConnectionHealthMonitorService();
        var profile = new ConnectionProfile
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Local",
            Provider = DatabaseProvider.Postgres,
            Host = "localhost",
            Port = 5432,
            Database = "db",
            Username = "u",
            Password = "p",
            TimeoutSeconds = 30,
        };

        EConnectionHealthStatus status = await service.EvaluateStatusAsync(
            profile,
            runTestAsync: (_, _, _, _) => Task.FromResult(new ConnectionTestResult(false, "fail", null)),
            degradedLatencyThresholdMs: 500);

        Assert.Equal(EConnectionHealthStatus.Offline, status);
    }
}


