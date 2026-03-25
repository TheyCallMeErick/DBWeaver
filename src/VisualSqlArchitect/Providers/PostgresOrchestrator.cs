using System.Data;
using System.Data.Common;
using Npgsql;

namespace VisualSqlArchitect.Providers;

/// <summary>
/// PostgreSQL implementation of IDbOrchestrator.
/// Uses pg_catalog system tables for richer metadata than INFORMATION_SCHEMA.
/// Compatible with Postgres 12+.
/// </summary>
public sealed class PostgresOrchestrator : Core.BaseDbOrchestrator
{
    public PostgresOrchestrator(Core.ConnectionConfig config) : base(config) { }

    public override Core.DatabaseProvider Provider => Core.DatabaseProvider.Postgres;

    // ── LIMIT-N wrapping ──────────────────────────────────────────────────────
    protected override string WrapWithLimit(string sql, int maxRows) =>
        $"SELECT * FROM ({sql}) AS __preview LIMIT {maxRows}";

    // ── Connection ────────────────────────────────────────────────────────────
    protected override async Task<DbConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var conn = new NpgsqlConnection(Config.BuildConnectionString());
        await conn.OpenAsync(ct);
        return conn;
    }

    // ── Tables ────────────────────────────────────────────────────────────────
    protected override async Task<IReadOnlyList<(string Schema, string Table)>>
        FetchTablesAsync(DbConnection conn, CancellationToken ct)
    {
        // Exclude pg_ system schemas and information_schema
        const string sql = """
            SELECT schemaname, tablename
            FROM   pg_catalog.pg_tables
            WHERE  schemaname NOT IN ('pg_catalog', 'information_schema')
            ORDER  BY schemaname, tablename
            """;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var result = new List<(string, string)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add((reader.GetString(0), reader.GetString(1)));

        return result;
    }

    // ── Columns ───────────────────────────────────────────────────────────────
    protected override async Task<IReadOnlyList<Core.ColumnSchema>>
        FetchColumnsAsync(DbConnection conn, string schema, string table, CancellationToken ct)
    {
        // pg_catalog query returns PK flag and FK target in a single pass
        const string sql = """
            SELECT
                a.attname                                   AS column_name,
                pg_catalog.format_type(a.atttypid, a.atttypmod) AS data_type,
                NOT a.attnotnull                             AS is_nullable,
                a.atttypmod - 4                              AS max_length,     -- varchar length hack
                -- Is part of PK?
                EXISTS (
                    SELECT 1 FROM pg_index i
                    WHERE  i.indrelid = a.attrelid
                      AND  i.indisprimary
                      AND  a.attnum = ANY(i.indkey)
                ) AS is_pk,
                -- FK referenced table
                (
                    SELECT c2.relname
                    FROM   pg_constraint fkc
                    JOIN   pg_class      c2   ON c2.oid = fkc.confrelid
                    WHERE  fkc.conrelid  = a.attrelid
                      AND  fkc.contype   = 'f'
                      AND  a.attnum      = ANY(fkc.conkey)
                    LIMIT 1
                ) AS fk_table
            FROM   pg_catalog.pg_attribute a
            JOIN   pg_catalog.pg_class     c ON c.oid = a.attrelid
            JOIN   pg_catalog.pg_namespace n ON n.oid = c.relnamespace
            WHERE  n.nspname = @schema
              AND  c.relname = @table
              AND  a.attnum  > 0                   -- skip system columns
              AND  NOT a.attisdropped
            ORDER  BY a.attnum
            """;

        await using var cmd = (NpgsqlCommand)conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table",  table);

        var columns = new List<Core.ColumnSchema>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            // atttypmod - 4 is negative for types without a length; guard it
            var rawLen = reader.GetInt32(3);
            int? maxLen = rawLen > 0 ? rawLen : null;

            columns.Add(new Core.ColumnSchema(
                Name:            reader.GetString(0),
                DataType:        reader.GetString(1),
                IsNullable:      reader.GetBoolean(2),
                MaxLength:       maxLen,
                IsPrimaryKey:    reader.GetBoolean(4),
                IsForeignKey:    !reader.IsDBNull(5),
                ForeignKeyTable: reader.IsDBNull(5) ? null : reader.GetString(5)
            ));
        }

        return columns;
    }
}
