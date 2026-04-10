namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorExecutionFeedback
{
    public required string StatusText { get; init; }

    public string? DetailText { get; init; }

    public required bool HasError { get; init; }
}

