using DBWeaver.Core;
using DBWeaver.Metadata;

namespace DBWeaver.UI.Services.ConnectionManager;

public sealed class ConnectionCanvasPromptCoordinator : IConnectionCanvasPromptCoordinator
{
    public ConnectionCanvasPromptState Open(DbMetadata metadata, ConnectionConfig config) =>
        new(IsVisible: true, PendingMetadata: metadata, PendingConfig: config);

    public ConnectionCanvasPromptState Close() =>
        new(IsVisible: false, PendingMetadata: null, PendingConfig: null);

    public bool ShouldAddDismissWarning(bool dismissedByUser, bool isPromptVisible, DbMetadata? pendingMetadata) =>
        dismissedByUser && isPromptVisible && pendingMetadata is not null;
}

