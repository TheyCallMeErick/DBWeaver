namespace DBWeaver.UI.Services.SqlEditor;

public sealed record SqlStatement(
    string Sql,
    int StartLine,
    int EndLine,
    StatementKind Kind
);
