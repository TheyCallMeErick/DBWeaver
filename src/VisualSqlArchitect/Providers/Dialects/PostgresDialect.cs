namespace VisualSqlArchitect.Providers.Dialects;

/// <summary>
/// Implementação de ISqlDialect para PostgreSQL.
/// Usa Information Schema views compatível com PostgreSQL 9.6+
/// </summary>
public sealed class PostgresDialect : ISqlDialect
{
    public string GetTablesQuery() =>
        @"
            SELECT
                table_schema,
                table_name
            FROM
                information_schema.tables
            WHERE
                table_type = 'BASE TABLE'
                AND table_schema NOT IN ('pg_catalog', 'information_schema')
            ORDER BY
                table_schema, table_name
        ";

    public string GetColumnsQuery() =>
        @"
            SELECT
                column_name,
                udt_name AS data_type,
                is_nullable::boolean,
                CASE
                    WHEN column_name IN (
                        SELECT a.attname
                        FROM pg_index i
                        JOIN pg_attribute a ON a.attrelid = i.indrelid
                            AND a.attnum = ANY(i.indkey)
                        WHERE i.indisprimary
                            AND i.indrelid = (@schema || '.' || @table)::regclass
                    ) THEN true
                    ELSE false
                END AS is_primary_key
            FROM
                information_schema.columns
            WHERE
                table_schema = @schema
                AND table_name = @table
            ORDER BY
                ordinal_position
        ";

    public string GetPrimaryKeysQuery() =>
        @"
            SELECT
                a.attname AS column_name
            FROM
                pg_index i
                JOIN pg_attribute a ON a.attrelid = i.indrelid
                    AND a.attnum = ANY(i.indkey)
            WHERE
                i.indisprimary
                AND i.indrelid = (@schema || '.' || @table)::regclass
        ";

    public string GetForeignKeysQuery() =>
        @"
            SELECT
                kcu.column_name,
                ccu.table_name AS referenced_table,
                ccu.column_name AS referenced_column
            FROM
                information_schema.table_constraints AS tc
                JOIN information_schema.key_column_usage AS kcu
                    ON tc.constraint_name = kcu.constraint_name
                    AND tc.table_schema = kcu.table_schema
                JOIN information_schema.constraint_column_usage AS ccu
                    ON ccu.constraint_name = tc.constraint_name
                    AND ccu.table_schema = tc.table_schema
            WHERE
                tc.constraint_type = 'FOREIGN KEY'
                AND tc.table_schema = @schema
                AND tc.table_name = @table
        ";

    public string WrapWithPreviewLimit(string baseQuery, int maxRows) =>
        $"{baseQuery} LIMIT {maxRows}";

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
