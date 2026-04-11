namespace DBWeaver.UI.Services.SqlEditor;

public sealed record SqlToken(
    SqlTokenKind Kind,
    string Value,
    int StartOffset,
    int EndOffset,
    bool IsComment);
