namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorFileOpenOutcome
{
    public required bool Success { get; init; }

    public required string StatusText { get; init; }

    public string? DetailText { get; init; }

    public required bool HasError { get; init; }

    public string? Content { get; init; }
}

