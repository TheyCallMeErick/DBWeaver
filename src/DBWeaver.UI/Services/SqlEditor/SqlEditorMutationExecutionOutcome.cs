namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorMutationExecutionOutcome
{
    public required SqlEditorResultSet Result { get; init; }

    public SqlEditorMutationConfirmationState? ConfirmationState { get; init; }

    public bool RequiresConfirmation => ConfirmationState is not null;
}

