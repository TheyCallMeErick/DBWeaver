namespace VisualSqlArchitect.Nodes;

/// <summary>
/// Metadata carried by a ColumnRef pin to preserve semantic column identity.
/// </summary>
public sealed record ColumnRefMeta(
    string ColumnName,
    string? TableAlias,
    PinDataType ScalarType,
    bool IsNullable
);

/// <summary>
/// Metadata carried by a ColumnSet pin to preserve ordered column schema.
/// </summary>
public sealed record ColumnSetMeta(IReadOnlyList<ColumnRefMeta> Columns);
