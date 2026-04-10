using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorMutationConfirmationState
{
    public required string StatementSql { get; init; }

    public required MutationGuardResult Guard { get; init; }

    public required SqlMutationDiffPreview DiffPreview { get; init; }

    public long? EstimatedRows { get; init; }
}

