using DBWeaver.Core;
using DBWeaver.Metadata;

namespace DBWeaver.UI.Services.ConnectionManager;

public readonly record struct ConnectionActivationResult(
    ConnectionActivationOutcome Outcome,
    ConnectionConfig? Config = null,
    DbMetadata? Metadata = null,
    bool ShouldOpenClearCanvasPrompt = false,
    string? FailureReason = null,
    Exception? FailureException = null);

