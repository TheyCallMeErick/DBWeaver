using System.Data.Common;
using Microsoft.Data.Sqlite;
using DBWeaver.Core;

namespace DBWeaver.Metadata.Inspectors;

/// <summary>
/// SQLite inspector based on sqlite_master and PRAGMA metadata.
/// </summary>
public sealed class SqliteInspector(ConnectionConfig config) : BaseInspector(config)
{
    public override DatabaseProvider Provider => DatabaseProvider.SQLite;

    protected override async Task<DbConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new SqliteConnection(Config.BuildConnectionString());
        await conn.OpenAsync(ct);

        await using DbCommand pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        await pragma.ExecuteNonQueryAsync(ct);

        return conn;
    }

    protected override async Task<string> GetServerVersionAsync(
        DbConnection conn,
        CancellationToken ct
    )
    {
        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT sqlite_version();";
        return (await cmd.ExecuteScalarAsync(ct))?.ToString() ?? "unknown";
    }

    protected override async Task<
        IReadOnlyList<(string Schema, string Name, TableKind Kind, long? RowCount, string? Comment)>
    > FetchAllTablesAsync(DbConnection conn, CancellationToken ct)
    {
        const string sql = """
            SELECT name, type
            FROM sqlite_master
            WHERE type IN ('table','view')
              AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
            """;

        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var result = new List<(string, string, TableKind, long?, string?)>();
        await using DbDataReader reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            string name = reader.GetString(0);
            string type = reader.GetString(1);
            TableKind kind = type.Equals("view", StringComparison.OrdinalIgnoreCase)
                ? TableKind.View
                : TableKind.Table;
            result.Add(("main", name, kind, null, null));
        }

        return result;
    }

    protected override async Task<IReadOnlyList<ColumnMetadata>> FetchColumnsAsync(
        DbConnection conn,
        string schema,
        string table,
        CancellationToken ct
    )
    {
        _ = schema;
        string escapedTable = EscapeIdentifier(table);

        HashSet<string> fkColumns = await GetForeignKeyColumnsAsync(conn, escapedTable, ct);
        HashSet<string> indexedColumns = await GetIndexedColumnsAsync(conn, escapedTable, ct);
        HashSet<string> uniqueColumns = await GetUniqueColumnsAsync(conn, escapedTable, ct);

        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{escapedTable}\");";

        var columns = new List<ColumnMetadata>();
        await using DbDataReader reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            // cid, name, type, notnull, dflt_value, pk
            string name = reader.GetString(1);
            string rawType = reader.IsDBNull(2) ? "text" : reader.GetString(2);
            string normalized = NormalizeType(rawType);
            (int? length, int? precision, int? scale) = ParseTypeArgs(rawType);

            columns.Add(
                new ColumnMetadata(
                    Name: name,
                    DataType: normalized,
                    NativeType: rawType,
                    IsNullable: reader.GetInt32(3) == 0,
                    IsPrimaryKey: reader.GetInt32(5) > 0,
                    IsForeignKey: fkColumns.Contains(name),
                    IsUnique: uniqueColumns.Contains(name),
                    IsIndexed: indexedColumns.Contains(name),
                    OrdinalPosition: reader.GetInt32(0) + 1,
                    DefaultValue: reader.IsDBNull(4) ? null : reader.GetString(4),
                    MaxLength: length,
                    Precision: precision,
                    Scale: scale
                )
            );
        }

        return columns;
    }

    protected override async Task<IReadOnlyList<IndexMetadata>> FetchIndexesAsync(
        DbConnection conn,
        string schema,
        string table,
        CancellationToken ct
    )
    {
        _ = schema;
        string escapedTable = EscapeIdentifier(table);

        await using DbCommand listCmd = conn.CreateCommand();
        listCmd.CommandText = $"PRAGMA index_list(\"{escapedTable}\");";

        var indexes = new List<IndexMetadata>();
        await using DbDataReader listReader = await listCmd.ExecuteReaderAsync(ct);
        while (await listReader.ReadAsync(ct))
        {
            // seq, name, unique, origin, partial
            string indexName = listReader.GetString(1);
            bool isUnique = listReader.GetInt32(2) == 1;
            bool isPrimaryKey = listReader.IsDBNull(3)
                ? false
                : listReader.GetString(3).Equals("pk", StringComparison.OrdinalIgnoreCase);
            IReadOnlyList<string> columns = await GetIndexColumnsAsync(conn, indexName, ct);

            indexes.Add(
                new IndexMetadata(
                    Name: indexName,
                    IsUnique: isUnique,
                    IsClustered: false,
                    IsPrimaryKey: isPrimaryKey,
                    Columns: columns
                )
            );
        }

        return indexes;
    }

    protected override async Task<IReadOnlyList<ForeignKeyRelation>> FetchForeignKeysAsync(
        DbConnection conn,
        CancellationToken ct
    )
    {
        IReadOnlyList<string> tables = await GetUserTablesAsync(conn, ct);
        var relations = new List<ForeignKeyRelation>();

        foreach (string table in tables)
        {
            string escapedTable = EscapeIdentifier(table);
            await using DbCommand fkCmd = conn.CreateCommand();
            fkCmd.CommandText = $"PRAGMA foreign_key_list(\"{escapedTable}\");";

            await using DbDataReader fkReader = await fkCmd.ExecuteReaderAsync(ct);
            while (await fkReader.ReadAsync(ct))
            {
                // id, seq, table, from, to, on_update, on_delete, match
                int fkId = fkReader.GetInt32(0);
                int seq = fkReader.GetInt32(1);
                string parentTable = fkReader.GetString(2);
                string childColumn = fkReader.GetString(3);
                string parentColumn = fkReader.IsDBNull(4) ? "rowid" : fkReader.GetString(4);
                string onUpdate = fkReader.IsDBNull(5) ? "NO ACTION" : fkReader.GetString(5);
                string onDelete = fkReader.IsDBNull(6) ? "NO ACTION" : fkReader.GetString(6);

                relations.Add(
                    new ForeignKeyRelation(
                        ConstraintName: $"fk_{table}_{fkId}",
                        ChildSchema: "main",
                        ChildTable: table,
                        ChildColumn: childColumn,
                        ParentSchema: "main",
                        ParentTable: parentTable,
                        ParentColumn: parentColumn,
                        OnDelete: ParseReferentialAction(onDelete),
                        OnUpdate: ParseReferentialAction(onUpdate),
                        OrdinalPosition: seq + 1
                    )
                );
            }
        }

        return relations;
    }

    protected override Task<IReadOnlyList<SequenceMetadata>> FetchSequencesAsync(
        DbConnection conn,
        CancellationToken ct
    )
    {
        _ = conn;
        _ = ct;
        return Task.FromResult<IReadOnlyList<SequenceMetadata>>([]);
    }

    private static async Task<IReadOnlyList<string>> GetUserTablesAsync(
        DbConnection conn,
        CancellationToken ct
    )
    {
        const string sql = """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
            """;

        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var tables = new List<string>();
        await using DbDataReader reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            tables.Add(reader.GetString(0));
        return tables;
    }

    private static async Task<HashSet<string>> GetForeignKeyColumnsAsync(
        DbConnection conn,
        string escapedTable,
        CancellationToken ct
    )
    {
        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA foreign_key_list(\"{escapedTable}\");";

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using DbDataReader reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            columns.Add(reader.GetString(3));

        return columns;
    }

    private static async Task<HashSet<string>> GetIndexedColumnsAsync(
        DbConnection conn,
        string escapedTable,
        CancellationToken ct
    )
    {
        await using DbCommand indexList = conn.CreateCommand();
        indexList.CommandText = $"PRAGMA index_list(\"{escapedTable}\");";

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using DbDataReader listReader = await indexList.ExecuteReaderAsync(ct);
        while (await listReader.ReadAsync(ct))
        {
            string indexName = listReader.GetString(1);
            IReadOnlyList<string> indexColumns = await GetIndexColumnsAsync(conn, indexName, ct);
            foreach (string column in indexColumns)
                columns.Add(column);
        }

        return columns;
    }

    private static async Task<HashSet<string>> GetUniqueColumnsAsync(
        DbConnection conn,
        string escapedTable,
        CancellationToken ct
    )
    {
        await using DbCommand indexList = conn.CreateCommand();
        indexList.CommandText = $"PRAGMA index_list(\"{escapedTable}\");";

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using DbDataReader listReader = await indexList.ExecuteReaderAsync(ct);
        while (await listReader.ReadAsync(ct))
        {
            bool isUnique = listReader.GetInt32(2) == 1;
            if (!isUnique)
                continue;

            string indexName = listReader.GetString(1);
            IReadOnlyList<string> indexColumns = await GetIndexColumnsAsync(conn, indexName, ct);
            foreach (string column in indexColumns)
                columns.Add(column);
        }

        return columns;
    }

    private static async Task<IReadOnlyList<string>> GetIndexColumnsAsync(
        DbConnection conn,
        string indexName,
        CancellationToken ct
    )
    {
        string escapedIndex = EscapeIdentifier(indexName);
        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA index_info(\"{escapedIndex}\");";

        var columns = new List<(int SeqNo, string Name)>();
        await using DbDataReader reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            columns.Add((reader.GetInt32(0), reader.GetString(2)));

        return columns.OrderBy(c => c.SeqNo).Select(c => c.Name).ToList();
    }

    private static string EscapeIdentifier(string value) => value.Replace("\"", "\"\"");

    private static string NormalizeType(string nativeType)
    {
        string t = nativeType.ToLowerInvariant();
        if (t.Contains("int"))
            return "integer";
        if (t.Contains("char") || t.Contains("clob") || t.Contains("text"))
            return "text";
        if (t.Contains("real") || t.Contains("floa") || t.Contains("doub"))
            return "float";
        if (t.Contains("dec") || t.Contains("num"))
            return "decimal";
        if (t.Contains("blob"))
            return "binary";
        if (t.Contains("bool"))
            return "boolean";
        if (t.Contains("date") || t.Contains("time"))
            return "datetime";
        return t;
    }

    private static (int? Length, int? Precision, int? Scale) ParseTypeArgs(string nativeType)
    {
        int open = nativeType.IndexOf('(');
        int close = nativeType.IndexOf(')');
        if (open < 0 || close <= open)
            return (null, null, null);

        string args = nativeType.Substring(open + 1, close - open - 1);
        string[] parts = args.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1 && int.TryParse(parts[0], out int length))
            return (length, null, null);
        if (parts.Length == 2
            && int.TryParse(parts[0], out int precision)
            && int.TryParse(parts[1], out int scale))
            return (null, precision, scale);

        return (null, null, null);
    }
}
