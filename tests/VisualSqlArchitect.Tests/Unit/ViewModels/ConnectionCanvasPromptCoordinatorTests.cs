using DBWeaver.UI.Services.ConnectionManager;
using DBWeaver.UI.Services.Benchmark;
using DBWeaver.Core;
using DBWeaver.Metadata;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class ConnectionCanvasPromptCoordinatorTests
{
    [Fact]
    public void Open_ReturnsVisibleStateWithPendingPayload()
    {
        var coordinator = new ConnectionCanvasPromptCoordinator();
        DbMetadata metadata = BuildMetadata();
        ConnectionConfig config = BuildConfig();

        ConnectionCanvasPromptState state = coordinator.Open(metadata, config);

        Assert.True(state.IsVisible);
        Assert.Same(metadata, state.PendingMetadata);
        Assert.Same(config, state.PendingConfig);
    }

    [Fact]
    public void Close_ClearsPendingPayloadAndHidesPrompt()
    {
        var coordinator = new ConnectionCanvasPromptCoordinator();

        ConnectionCanvasPromptState state = coordinator.Close();

        Assert.False(state.IsVisible);
        Assert.Null(state.PendingMetadata);
        Assert.Null(state.PendingConfig);
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    public void ShouldAddDismissWarning_RequiresAllConditions(
        bool dismissedByUser,
        bool isPromptVisible,
        bool hasPendingMetadata,
        bool expected)
    {
        var coordinator = new ConnectionCanvasPromptCoordinator();
        DbMetadata? metadata = hasPendingMetadata ? BuildMetadata() : null;

        bool result = coordinator.ShouldAddDismissWarning(dismissedByUser, isPromptVisible, metadata);

        Assert.Equal(expected, result);
    }

    private static ConnectionConfig BuildConfig() =>
        new(DatabaseProvider.Postgres, "localhost", 5432, "db", "u", "p");

    private static DbMetadata BuildMetadata() =>
        new(
            DatabaseName: "db",
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "16",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [],
            AllForeignKeys: []);
}


