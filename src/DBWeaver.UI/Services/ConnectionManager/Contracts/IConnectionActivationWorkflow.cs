using DBWeaver.Core;
using DBWeaver.Metadata;
using DBWeaver.UI.Services;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.ConnectionManager;

public interface IConnectionActivationWorkflow
{
    Task<ConnectionActivationResult> ExecuteAsync(
        ConnectionProfile profile,
        SearchMenuViewModel? searchMenu,
        CanvasViewModel? canvas,
        Func<ConnectionConfig, SearchMenuViewModel, CancellationToken, Task<DbMetadata?>> loadMetadataAsync,
        CancellationToken ct);
}

