namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorTabCloseOutcome
{
    public required SqlEditorTabCloseAction Action { get; init; }

    public string? StatusText { get; init; }

    public string? DetailText { get; init; }

    public bool HasError { get; init; }
}

