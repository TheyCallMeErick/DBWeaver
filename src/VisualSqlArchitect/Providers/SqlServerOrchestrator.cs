using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace VisualSqlArchitect.Providers;

/// <summary>
/// SQL Server implementation of IDbOrchestrator.
/// Schema discovery relies on INFORMATION_SCHEMA views for broad compatibility
/// (SQL Server 2012+ / Azure SQL).
/// </summary>
public sealed class SqlServerOrchestrator : Core.BaseDbOrchestrator
{
    public SqlServerOrchestrator(Core.ConnectionConfig config) : base(config) { }

    public override Core.DatabaseProvider Provider => Core.DatabaseProvider.SqlServer;

    // ── TOP-N wrapping (SQL Server style) ─────────────────────────────────────
    protected override string WrapWithLimit(string sql, int maxRows) =>
        $"SELECT TOP {maxRows} * FROM ({sql}) AS __preview";

    // ── Connection ────────────────────────────────────────────────────────────
    protected override async Task<DbConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var conn = new SqlConnection(Config.BuildConnectionString());
        await conn.OpenAsync(ct);
        return conn;
    }

    // ── Tables ────────────────────────────────────────────────────────────────
    protected override async Task<IReadOnlyList<(string Schema, string Table)>>
        FetchTablesAsync(DbConnection conn, CancellationToken ct)
    {
        const string sql = """
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM   INFORMATION_SCHEMA.TABLES
            WHERE  TABLE_TYPE = 'BASE TABLE'
            ORDER  BY TABLE_SCHEMA, TABLE_NAME
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
        const string sql = """
            SELECT
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH,
                -- Is Primary Key?
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PK,
                -- FK target table
                fk_ref.TABLE_NAME AS FK_TABLE
            FROM INFORMATION_SCHEMA.COLUMNS c
            -- PK detection
            LEFT JOIN (
                SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                FROM   INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN   INFORMATION_SCHEMA.KEY_COLUMN_USAGE  ku
                       ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                       AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                WHERE  tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk ON pk.TABLE_SCHEMA = c.TABLE_SCHEMA
                 AND pk.TABLE_NAME  = c.TABLE_NAME
                 AND pk.COLUMN_NAME = c.COLUMN_NAME
            -- FK detection
            LEFT JOIN (
                SELECT
                    ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME,
                    rku.TABLE_NAME AS FK_REF_TABLE
                FROM   INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
                JOIN   INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                       ON rc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                JOIN   INFORMATION_SCHEMA.KEY_COLUMN_USAGE rku
                       ON rc.UNIQUE_CONSTRAINT_NAME = rku.CONSTRAINT_NAME
                          AND ku.ORDINAL_POSITION = rku.ORDINAL_POSITION
            ) fk_ref ON fk_ref.TABLE_SCHEMA = c.TABLE_SCHEMA
                     AND fk_ref.TABLE_NAME  = c.TABLE_NAME
                     AND fk_ref.COLUMN_NAME = c.COLUMN_NAME
            WHERE c.TABLE_SCHEMA = @schema
              AND c.TABLE_NAME   = @table
            ORDER BY c.ORDINAL_POSITION
            """;

        await using var cmd = (SqlCommand)conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table",  table);

        var columns = new List<Core.ColumnSchema>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            columns.Add(new Core.ColumnSchema(
                Name:           reader.GetString(0),
                DataType:       reader.GetString(1),
                IsNullable:     reader.GetString(2) == "YES",
                MaxLength:      reader.IsDBNull(3) ? null : (int?)reader.GetInt32(3),
                IsPrimaryKey:   reader.GetInt32(4) == 1,
                IsForeignKey:   !reader.IsDBNull(5),
                ForeignKeyTable: reader.IsDBNull(5) ? null : reader.GetString(5)
            ));
        }

        return columns;
    }
}
