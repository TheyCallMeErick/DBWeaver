namespace DBWeaver.UI.ViewModels;

public sealed class SqlEditorSchemaTableItem
{
    public required string Schema { get; init; }
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required IReadOnlyList<SqlEditorSchemaColumnItem> Columns { get; init; }
}
