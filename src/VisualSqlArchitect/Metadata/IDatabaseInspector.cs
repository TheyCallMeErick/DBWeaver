using System.Data.Common;
using VisualSqlArchitect.Core;

namespace VisualSqlArchitect.Metadata;

// ─── Inspector interface ──────────────────────────────────────────────────────

/// <summary>
/// Deep database introspection contract.
/// Implementations query provider-specific system catalogs and return a
/// normalised <see cref="DbMetadata"/> object.
/// </summary>
public interface IDatabaseInspector
{
    DatabaseProvider Provider { get; }

    /// <summary>
    /// Builds the complete <see cref="DbMetadata"/> for the connected database,
    /// including tables, columns, indexes, FK graph and row-count estimates.
    /// </summary>
    Task<DbMetadata> InspectAsync(CancellationToken ct = default);

    /// <summary>
    /// Re-inspects a single table in-place (useful for live-refresh when the
    /// user right-clicks a node on the canvas).
    /// </summary>
    Task<TableMetadata> InspectTableAsync(
        string schema, string table, CancellationToken ct = default);

    /// <summary>
    /// Fetches only the FK graph — fast path for Auto-Join without a full reload.
    /// </summary>
    Task<IReadOnlyList<ForeignKeyRelation>> GetForeignKeysAsync(CancellationToken ct = default);
}

// ─── Abstract base with shared normalisation helpers ─────────────────────────

public abstract class BaseInspector : IDatabaseInspector
{
    protected readonly ConnectionConfig Config;
    public abstract DatabaseProvider Provider { get; }

    protected BaseInspector(ConnectionConfig config) => Config = config;

    // ── Template method ───────────────────────────────────────────────────────

    public async Task<DbMetadata> InspectAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);

        var version  = await GetServerVersionAsync(conn, ct);
        var rawTables = await FetchAllTablesAsync(conn, ct);
        var allFks   = await FetchForeignKeysAsync(conn, ct);

        // Index FKs by table for O(1) lookup during table assembly
        var fksByChild  = allFks.ToLookup(r => r.ChildFullTable,  StringComparer.OrdinalIgnoreCase);
        var fksByParent = allFks.ToLookup(r => r.ParentFullTable, StringComparer.OrdinalIgnoreCase);

        var tableMetaList = new List<TableMetadata>();

        foreach (var (schema, name, kind, rowCount) in rawTables)
        {
            ct.ThrowIfCancellationRequested();

            var columns = await FetchColumnsAsync(conn, schema, name, ct);
            var indexes = await FetchIndexesAsync(conn, schema, name, ct);

            var fullName = string.IsNullOrEmpty(schema) ? name : $"{schema}.{name}";
            tableMetaList.Add(new TableMetadata(
                Schema:              schema,
                Name:                name,
                Kind:                kind,
                EstimatedRowCount:   rowCount,
                Columns:             columns,
                Indexes:             indexes,
                OutboundForeignKeys: fksByChild[fullName].ToList(),
                InboundForeignKeys:  fksByParent[fullName].ToList()
            ));
        }

        var schemas = tableMetaList
            .GroupBy(t => t.Schema, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key)
            .Select(g => new SchemaMetadata(g.Key,
                g.OrderBy(t => t.Name).ToList()))
            .ToList();

        return new DbMetadata(
            DatabaseName:  Config.Database,
            Provider:      Provider,
            ServerVersion: version,
            CapturedAt:    DateTimeOffset.UtcNow,
            Schemas:       schemas,
            AllForeignKeys: allFks
        );
    }

    public async Task<TableMetadata> InspectTableAsync(
        string schema, string table, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        var columns  = await FetchColumnsAsync(conn, schema, table, ct);
        var indexes  = await FetchIndexesAsync(conn, schema, table, ct);
        var allFks   = await FetchForeignKeysAsync(conn, ct);
        var fullName = string.IsNullOrEmpty(schema) ? table : $"{schema}.{table}";

        return new TableMetadata(
            Schema:              schema,
            Name:                table,
            Kind:                TableKind.Table,
            EstimatedRowCount:   null,
            Columns:             columns,
            Indexes:             indexes,
            OutboundForeignKeys: allFks.Where(r => r.ChildFullTable.Equals(fullName,  StringComparison.OrdinalIgnoreCase)).ToList(),
            InboundForeignKeys:  allFks.Where(r => r.ParentFullTable.Equals(fullName, StringComparison.OrdinalIgnoreCase)).ToList()
        );
    }

    public async Task<IReadOnlyList<ForeignKeyRelation>> GetForeignKeysAsync(
        CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        return await FetchForeignKeysAsync(conn, ct);
    }

    // ── Abstract hooks (each provider implements these) ───────────────────────

    protected abstract Task<DbConnection> OpenAsync(CancellationToken ct);

    protected abstract Task<string> GetServerVersionAsync(DbConnection conn, CancellationToken ct);

    /// <summary>Returns (schema, table, kind, estimatedRows) tuples.</summary>
    protected abstract Task<IReadOnlyList<(string Schema, string Name, TableKind Kind, long? RowCount)>>
        FetchAllTablesAsync(DbConnection conn, CancellationToken ct);

    protected abstract Task<IReadOnlyList<ColumnMetadata>>
        FetchColumnsAsync(DbConnection conn, string schema, string table, CancellationToken ct);

    protected abstract Task<IReadOnlyList<IndexMetadata>>
        FetchIndexesAsync(DbConnection conn, string schema, string table, CancellationToken ct);

    protected abstract Task<IReadOnlyList<ForeignKeyRelation>>
        FetchForeignKeysAsync(DbConnection conn, CancellationToken ct);

    // ── Shared normalisation ──────────────────────────────────────────────────

    protected static ReferentialAction ParseReferentialAction(string? raw) =>
        raw?.ToUpperInvariant() switch
        {
            "CASCADE"     => ReferentialAction.Cascade,
            "SET NULL"    => ReferentialAction.SetNull,
            "SET DEFAULT" => ReferentialAction.SetDefault,
            "RESTRICT"    => ReferentialAction.Restrict,
            _             => ReferentialAction.NoAction
        };
}
