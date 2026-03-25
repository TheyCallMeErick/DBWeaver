using System.Data;
using System.Data.Common;
using MySqlConnector;

namespace VisualSqlArchitect.Providers;

/// <summary>
/// MySQL / MariaDB implementation of IDbOrchestrator.
/// Uses INFORMATION_SCHEMA (MySQL 5.7+ / MariaDB 10.2+).
/// </summary>
public sealed class MySqlOrchestrator : Core.BaseDbOrchestrator
{
    public MySqlOrchestrator(Core.ConnectionConfig config) : base(config) { }

    public override Core.DatabaseProvider Provider => Core.DatabaseProvider.MySql;

    // ── LIMIT-N wrapping ──────────────────────────────────────────────────────
    protected override string WrapWithLimit(string sql, int maxRows) =>
        $"SELECT * FROM ({sql}) AS __preview LIMIT {maxRows}";

    // ── Connection ────────────────────────────────────────────────────────────
    protected override async Task<DbConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var conn = new MySqlConnection(Config.BuildConnectionString());
        await conn.OpenAsync(ct);
        return conn;
    }

    // ── Tables ────────────────────────────────────────────────────────────────
    protected override async Task<IReadOnlyList<(string Schema, string Table)>>
        FetchTablesAsync(DbConnection conn, CancellationToken ct)
    {
        // In MySQL, TABLE_SCHEMA == Database name; we expose it as schema for consistency.
        const string sql = """
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM   INFORMATION_SCHEMA.TABLES
            WHERE  TABLE_TYPE   = 'BASE TABLE'
              AND  TABLE_SCHEMA = @db
            ORDER  BY TABLE_NAME
            """;

        await using var cmd = (MySqlCommand)conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@db", Config.Database);

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
        const string sql = """
            SELECT
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH,
                -- PK detection via COLUMN_KEY = 'PRI'
                CASE WHEN c.COLUMN_KEY = 'PRI' THEN 1 ELSE 0 END AS IS_PK,
                -- FK target table
                fk.REFERENCED_TABLE_NAME
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT
                    kcu.TABLE_SCHEMA, kcu.TABLE_NAME,
                    kcu.COLUMN_NAME,  kcu.REFERENCED_TABLE_NAME
                FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                     ON  kcu.CONSTRAINT_NAME  = tc.CONSTRAINT_NAME
                     AND kcu.TABLE_SCHEMA     = tc.TABLE_SCHEMA
                WHERE tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
                  AND kcu.REFERENCED_TABLE_NAME IS NOT NULL
            ) fk ON  fk.TABLE_SCHEMA = c.TABLE_SCHEMA
                 AND fk.TABLE_NAME   = c.TABLE_NAME
                 AND fk.COLUMN_NAME  = c.COLUMN_NAME
            WHERE c.TABLE_SCHEMA = @schema
              AND c.TABLE_NAME   = @table
            ORDER BY c.ORDINAL_POSITION
            """;

        await using var cmd = (MySqlCommand)conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table",  table);

        var columns = new List<Core.ColumnSchema>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            columns.Add(new Core.ColumnSchema(
                Name:            reader.GetString(0),
                DataType:        reader.GetString(1),
                IsNullable:      reader.GetString(2) == "YES",
                MaxLength:       reader.IsDBNull(3) ? null : (int?)reader.GetInt32(3),
                IsPrimaryKey:    reader.GetInt32(4) == 1,
                IsForeignKey:    !reader.IsDBNull(5),
                ForeignKeyTable: reader.IsDBNull(5) ? null : reader.GetString(5)
            ));
        }

        return columns;
    }
}
