using DBWeaver.Core;
using DBWeaver.Metadata;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.ConnectionManager;

public sealed class ConnectionActivationWorkflow : IConnectionActivationWorkflow
{
    public async Task<ConnectionActivationResult> ExecuteAsync(
        ConnectionProfile profile,
        SearchMenuViewModel? searchMenu,
        CanvasViewModel? canvas,
        Func<ConnectionConfig, SearchMenuViewModel, CancellationToken, Task<DbMetadata?>> loadMetadataAsync,
        CancellationToken ct)
    {
        if (searchMenu is null)
        {
            return new ConnectionActivationResult(
                Outcome: ConnectionActivationOutcome.SearchMenuUnavailable,
                FailureReason: "search menu not initialized");
        }

        ConnectionConfig config = profile.ToConnectionConfig();

        try
        {
            DbMetadata? metadata = await loadMetadataAsync(config, searchMenu, ct);
            if (canvas is not null && metadata is not null)
            {
                canvas.SetDatabaseContext(metadata, config);

                return new ConnectionActivationResult(
                    Outcome: ConnectionActivationOutcome.Connected,
                    Config: config,
                    Metadata: metadata,
                    ShouldOpenClearCanvasPrompt: !canvas.IsCanvasEmpty);
            }

            return new ConnectionActivationResult(
                Outcome: ConnectionActivationOutcome.MetadataUnavailable,
                Config: config);
        }
        catch (OperationCanceledException)
        {
            return new ConnectionActivationResult(ConnectionActivationOutcome.Cancelled, Config: config);
        }
        catch (Exception ex)
        {
            return new ConnectionActivationResult(
                Outcome: ConnectionActivationOutcome.Failed,
                Config: config,
                FailureException: ex);
        }
    }
}

