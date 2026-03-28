namespace VisualSqlArchitect.Providers.Dialects;

/// <summary>
/// Implementação de ISqlDialect para MySQL.
/// Usa INFORMATION_SCHEMA views compatível com MySQL 5.7+
/// </summary>
public sealed class MySqlDialect : ISqlDialect
{
    public string GetTablesQuery() =>
        @"
            SELECT
                TABLE_SCHEMA,
                TABLE_NAME
            FROM
                INFORMATION_SCHEMA.TABLES
            WHERE
                TABLE_TYPE = 'BASE TABLE'
                AND TABLE_SCHEMA NOT IN ('mysql', 'information_schema', 'performance_schema', 'sys')
            ORDER BY
                TABLE_SCHEMA, TABLE_NAME
        ";

    public string GetColumnsQuery() =>
        @"
            SELECT
                COLUMN_NAME,
                COLUMN_TYPE AS DATA_TYPE,
                CASE WHEN IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS IS_NULLABLE,
                CASE WHEN COLUMN_KEY = 'PRI' THEN 1 ELSE 0 END AS IS_PRIMARY_KEY
            FROM
                INFORMATION_SCHEMA.COLUMNS
            WHERE
                TABLE_SCHEMA = @schema
                AND TABLE_NAME = @table
            ORDER BY
                ORDINAL_POSITION
        ";

    public string GetPrimaryKeysQuery() =>
        @"
            SELECT
                COLUMN_NAME
            FROM
                INFORMATION_SCHEMA.COLUMNS
            WHERE
                TABLE_SCHEMA = @schema
                AND TABLE_NAME = @table
                AND COLUMN_KEY = 'PRI'
        ";

    public string GetForeignKeysQuery() =>
        @"
            SELECT
                COLUMN_NAME,
                REFERENCED_TABLE_NAME AS REFERENCED_TABLE,
                REFERENCED_COLUMN_NAME AS REFERENCED_COLUMN
            FROM
                INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE
                TABLE_SCHEMA = @schema
                AND TABLE_NAME = @table
                AND REFERENCED_TABLE_NAME IS NOT NULL
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
        if (offset.HasValue)
            parts.Add($"OFFSET {offset.Value}");
        return string.Join(" ", parts);
    }

    public string QuoteIdentifier(string identifier) =>
        $"`{identifier.Replace("`", "``")}`";
}
