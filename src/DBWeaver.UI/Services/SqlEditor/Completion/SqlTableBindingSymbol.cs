namespace DBWeaver.UI.Services.SqlEditor;

public sealed record SqlTableBindingSymbol(
    string TableRef,
    string Alias,
    bool IsCte,
    bool IsSubquery,
    int Order);
