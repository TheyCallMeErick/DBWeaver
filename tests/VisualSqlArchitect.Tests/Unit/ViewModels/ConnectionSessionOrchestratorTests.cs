using DBWeaver.UI.Services.ConnectionManager;
using DBWeaver.UI.Services.Benchmark;
using DBWeaver.Core;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class ConnectionSessionOrchestratorTests
{
    [Fact]
    public void BeginConnect_WithNullProfile_DoesNotStart()
    {
        var orchestrator = new ConnectionSessionOrchestrator();

        ConnectionConnectState state = orchestrator.BeginConnect(null, existingConnectCts: null);

        Assert.False(state.Started);
        Assert.Null(state.Profile);
    }

    [Fact]
    public void BeginConnect_WithProfile_StartsAndAllocatesToken()
    {
        var orchestrator = new ConnectionSessionOrchestrator();
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
        };

        ConnectionConnectState state = orchestrator.BeginConnect(profile, existingConnectCts: null);

        Assert.True(state.Started);
        Assert.Same(profile, state.Profile);
        Assert.NotNull(state.ConnectCts);
        Assert.Equal(profile.Id, state.ActiveProfileId);
        Assert.True(state.IsConnecting);

        state.ConnectCts?.Dispose();
    }

    [Fact]
    public void BeginDisconnect_ClearsConnectionState()
    {
        var orchestrator = new ConnectionSessionOrchestrator();
        using var cts = new CancellationTokenSource();

        ConnectionDisconnectState state = orchestrator.BeginDisconnect(cts);

        Assert.Null(state.ConnectCts);
        Assert.False(state.IsConnecting);
        Assert.Null(state.ActiveProfileId);
    }
}


