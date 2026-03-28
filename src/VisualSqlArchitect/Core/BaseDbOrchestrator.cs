using System.Data;
using System.Data.Common;
using System.Diagnostics;
using VisualSqlArchitect.Providers.Dialects;
using VisualSqlArchitect.Metadata;

namespace VisualSqlArchitect.Core;

/// <summary>
/// Provides the common scaffolding (timing, preview capping, safe disposal)
/// shared across all provider implementations.
/// Concrete classes only need to supply: a live connection, and provider-specific
/// schema queries via <see cref="FetchTablesAsync"/> / <see cref="FetchColumnsAsync"/>.
/// </summary>
public abstract class BaseDbOrchestrator(ConnectionConfig config) : IDbOrchestrator
{
    private bool _disposed;

    public abstract DatabaseProvider Provider { get; }
    public ConnectionConfig Config { get; } =
        config ?? throw new ArgumentNullException(nameof(config));

    // ── Connection factory (implementors create a fresh, open connection) ─────
    protected abstract Task<DbConnection> OpenConnectionAsync(CancellationToken ct);

    // ── Dialect factory (implementors provide provider-specific SQL dialect) ────
    protected abstract ISqlDialect GetDialect();

    // ── Metadata query provider factory ───────────────────────────────────────
    protected abstract IMetadataQueryProvider GetMetadataQueryProvider();

    // ── Schema hooks ──────────────────────────────────────────────────────────
    protected virtual async Task<IReadOnlyList<(string Schema, string Table)>> FetchTablesAsync(
        DbConnection conn,
        CancellationToken ct
    )
    {
        var provider = GetMetadataQueryProvider();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = provider.GetTablesQuery();

        var dt = new DataTable();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        dt.Load(reader);

        return provider.ParseTables(dt);
    }

    protected virtual async Task<IReadOnlyList<ColumnSchema>> FetchColumnsAsync(
        DbConnection conn,
        string schema,
        string table,
        CancellationToken ct
    )
    {
        var provider = GetMetadataQueryProvider();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = provider.GetColumnsQuery();
        var schemaParam = cmd.CreateParameter();
        schemaParam.ParameterName = "@schema";
        schemaParam.Value = schema;
        cmd.Parameters.Add(schemaParam);
        
        var tableParam = cmd.CreateParameter();
        tableParam.ParameterName = "@table";
        tableParam.Value = table;
        cmd.Parameters.Add(tableParam);

        var dt = new DataTable();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        dt.Load(reader);

        return provider.ParseColumns(dt);
    }

    // ── IDbOrchestrator ───────────────────────────────────────────────────────

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using DbConnection conn = await OpenConnectionAsync(ct);
            sw.Stop();
            return new ConnectionTestResult(true, Latency: sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConnectionTestResult(false, ex.Message, sw.Elapsed);
        }
    }

    public async Task<DatabaseSchema> GetSchemaAsync(CancellationToken ct = default)
    {
        await using DbConnection conn = await OpenConnectionAsync(ct);
        IReadOnlyList<(string Schema, string Table)> tables = await FetchTablesAsync(conn, ct);

        var tableSchemas = new List<TableSchema>(tables.Count);
        foreach ((string schema, string table) in tables)
        {
            ct.ThrowIfCancellationRequested();
            IReadOnlyList<ColumnSchema> columns = await FetchColumnsAsync(conn, schema, table, ct);
            tableSchemas.Add(new TableSchema(schema, table, columns));
        }

        return new DatabaseSchema(Config.Database, Provider, tableSchemas);
    }

    public async Task<PreviewResult> ExecutePreviewAsync(
        string sql,
        int maxRows = 200,
        CancellationToken ct = default
    )
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using DbConnection conn = await OpenConnectionAsync(ct);
            await using DbTransaction tx = await conn.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                ct
            );

            try
            {
                await using DbCommand cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = GetDialect().WrapWithPreviewLimit(sql, maxRows);
                cmd.CommandTimeout = Config.TimeoutSeconds;

                await using DbDataReader reader = await cmd.ExecuteReaderAsync(ct);
                var dt = new DataTable();
                dt.Load(reader);

                sw.Stop();
                return new PreviewResult(
                    true,
                    dt,
                    ExecutionTime: sw.Elapsed,
                    RowsAffected: dt.Rows.Count
                );
            }
            finally
            {
                // Always roll back — preview must never mutate data
                await tx.RollbackAsync(ct);
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new PreviewResult(false, ErrorMessage: ex.Message, ExecutionTime: sw.Elapsed);
        }
    }

    /// <summary>
    /// Cleanup resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;
}

