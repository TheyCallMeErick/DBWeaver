using DBWeaver.Core;
using DBWeaver.Metadata;

namespace DBWeaver.UI.Services.ConnectionManager;

public interface IConnectionCanvasPromptCoordinator
{
    ConnectionCanvasPromptState Open(DbMetadata metadata, ConnectionConfig config);

    ConnectionCanvasPromptState Close();

    bool ShouldAddDismissWarning(bool dismissedByUser, bool isPromptVisible, DbMetadata? pendingMetadata);
}

