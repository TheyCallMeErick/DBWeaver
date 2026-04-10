using DBWeaver.Core;
using DBWeaver.Metadata;

namespace DBWeaver.UI.Services.ConnectionManager;

public readonly record struct ConnectionCanvasPromptState(
    bool IsVisible,
    DbMetadata? PendingMetadata,
    ConnectionConfig? PendingConfig);

