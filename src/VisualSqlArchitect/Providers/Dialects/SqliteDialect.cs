namespace VisualSqlArchitect.Providers.Dialects;

/// <summary>
/// SQLite implementation of ISqlDialect.
/// SQLite uses sqlite_master system table for schema discovery and PRAGMA statements for metadata.
/// </summary>
public sealed class SqliteDialect : ISqlDialect
{
    public string GetTablesQuery() =>
        @"
            SELECT 'main' as TABLE_SCHEMA, name as TABLE_NAME
            FROM sqlite_master
            WHERE type='table'
              AND name NOT LIKE 'sqlite_%'
            ORDER BY name
        ";

    public string GetColumnsQuery() =>
        @"
            SELECT
                name as COLUMN_NAME,
                type as DATA_TYPE,
                CASE WHEN notnull = 0 THEN 1 ELSE 0 END AS IS_NULLABLE,
                CASE WHEN pk > 0 THEN 1 ELSE 0 END AS IS_PRIMARY_KEY
            FROM pragma_table_info(@table)
            ORDER BY cid
        ";

    public string GetPrimaryKeysQuery() =>
        @"
            SELECT name as COLUMN_NAME
            FROM pragma_table_info(@table)
            WHERE pk > 0
            ORDER BY pk
        ";

    public string GetForeignKeysQuery() =>
        @"
            SELECT
                'id' as id,
                table as REFERENCED_TABLE,
                to as REFERENCED_COLUMN
            FROM pragma_foreign_key_list(@table)
        ";

    public string WrapWithPreviewLimit(string baseQuery, int maxRows)
    {
        return $"SELECT * FROM ({baseQuery}) AS __preview LIMIT {maxRows}";
    }

    public string FormatPagination(int? limit, int? offset)
    {
        var parts = new List<string>();
        if (limit.HasValue)
            parts.Add($"LIMIT {limit.Value}");
        if (offset.HasValue && offset.Value > 0)
            parts.Add($"OFFSET {offset.Value}");
        return string.Join(" ", parts);
    }

    public string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"")}\"";
}
