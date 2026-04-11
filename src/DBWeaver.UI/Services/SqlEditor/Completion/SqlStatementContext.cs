namespace DBWeaver.UI.Services.SqlEditor;

public sealed record SqlStatementContext(
    IReadOnlyList<SqlToken> Tokens,
    int StartOffset,
    int EndOffset);
