namespace VisualSqlArchitect.Providers.Dialects;

/// <summary>
/// Implementação de ISqlDialect para SQL Server.
/// Usa INFORMATION_SCHEMA views compatível com SQL Server 2012+
/// </summary>
public sealed class SqlServerDialect : ISqlDialect
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
            ORDER BY
                TABLE_SCHEMA, TABLE_NAME
        ";

    public string GetColumnsQuery() =>
        @"
            SELECT
                COLUMN_NAME,
                DATA_TYPE,
                CASE WHEN IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS IS_NULLABLE,
                CASE
                    WHEN COLUMNPROPERTY(OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 1
                    THEN 1
                    ELSE 0
                END AS IS_PRIMARY_KEY
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
                INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE
                TABLE_SCHEMA = @schema
                AND TABLE_NAME = @table
                AND CONSTRAINT_NAME LIKE 'PK%'
        ";

    public string GetForeignKeysQuery() =>
        @"
            SELECT
                KCU1.COLUMN_NAME,
                KCU2.TABLE_NAME AS REFERENCED_TABLE,
                KCU2.COLUMN_NAME AS REFERENCED_COLUMN
            FROM
                INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS RC
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KCU1
                    ON RC.CONSTRAINT_NAME = KCU1.CONSTRAINT_NAME
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KCU2
                    ON RC.UNIQUE_CONSTRAINT_NAME = KCU2.CONSTRAINT_NAME
            WHERE
                KCU1.TABLE_SCHEMA = @schema
                AND KCU1.TABLE_NAME = @table
        ";

    public string WrapWithPreviewLimit(string baseQuery, int maxRows) =>
        $"SELECT TOP {maxRows} * FROM ({baseQuery}) AS __preview";

    public string FormatPagination(int? limit, int? offset)
    {
        if (!offset.HasValue)
            return limit.HasValue ? $"OFFSET 0 ROWS FETCH NEXT {limit} ROWS ONLY" : "";

        var parts = new List<string> { $"OFFSET {offset} ROWS" };
        if (limit.HasValue)
            parts.Add($"FETCH NEXT {limit} ROWS ONLY");

        return string.Join(" ", parts);
    }

    public string QuoteIdentifier(string identifier) =>
        $"[{identifier.Replace("]", "]]")}]";
}
