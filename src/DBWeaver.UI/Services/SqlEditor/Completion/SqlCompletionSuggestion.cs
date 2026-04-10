namespace DBWeaver.UI.Services.SqlEditor;

public sealed record SqlCompletionSuggestion(
    string Label,
    string InsertText,
    string? Detail,
    SqlCompletionKind Kind
);
