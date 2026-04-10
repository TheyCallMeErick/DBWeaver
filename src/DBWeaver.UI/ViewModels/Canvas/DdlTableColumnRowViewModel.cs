namespace DBWeaver.UI.ViewModels;

/// <summary>
/// Visual projection row for TableDefinition ERD preview.
/// </summary>
public sealed class DdlTableColumnRowViewModel(
    string name,
    string dataType,
    bool isNullable,
    bool isPrimaryKey,
    bool isForeignKey,
    bool isUnique
)
{
    public string Name { get; } = name;
    public string DataType { get; } = dataType;
    public bool IsNullable { get; } = isNullable;
    public bool IsPrimaryKey { get; } = isPrimaryKey;
    public bool IsForeignKey { get; } = isForeignKey;
    public bool IsUnique { get; } = isUnique;

    public string NullabilityLabel => IsNullable ? "NULL" : "NOT NULL";
}
