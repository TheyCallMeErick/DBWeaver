namespace DBWeaver.UI.ViewModels;

public sealed class SqlEditorSchemaColumnItem
{
    public required string Name { get; init; }
    public required string DataType { get; init; }
    public bool IsPrimaryKey { get; init; }
}
