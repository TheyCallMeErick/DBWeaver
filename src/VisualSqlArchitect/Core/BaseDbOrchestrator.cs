using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace VisualSqlArchitect.Core;

/// <summary>
/// Provides the common scaffolding (timing, preview capping, safe disposal)
/// shared across all provider implementations.
/// Concrete classes only need to supply: a live connection, and provider-specific
/// schema queries via <see cref="FetchTablesAsync"/> / <see cref="FetchColumnsAsync"/>.
/// </summary>
public abstract class BaseDbOrchestrator : IDbOrchestrator
{
    private bool _disposed;

    public abstract DatabaseProvider Provider { get; }
    public ConnectionConfig Config { get; }

    protected BaseDbOrchestrator(ConnectionConfig config)
        => Config = config ?? throw new ArgumentNullException(nameof(config));

    // ── Connection factory (implementors create a fresh, open connection) ─────
    protected abstract Task<DbConnection> OpenConnectionAsync(CancellationToken ct);

    // ── Schema hooks ──────────────────────────────────────────────────────────
    protected abstract Task<IReadOnlyList<(string Schema, string Table)>> FetchTablesAsync(
        DbConnection conn, CancellationToken ct);

    protected abstract Task<IReadOnlyList<ColumnSchema>> FetchColumnsAsync(
        DbConnection conn, string schema, string table, CancellationToken ct);

    // ── IDbOrchestrator ───────────────────────────────────────────────────────

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = await OpenConnectionAsync(ct);
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
        await using var conn = await OpenConnectionAsync(ct);
        var tables = await FetchTablesAsync(conn, ct);

        var tableSchemas = new List<TableSchema>(tables.Count);
        foreach (var (schema, table) in tables)
        {
            ct.ThrowIfCancellationRequested();
            var columns = await FetchColumnsAsync(conn, schema, table, ct);
            tableSchemas.Add(new TableSchema(schema, table, columns));
        }

        return new DatabaseSchema(Config.Database, Provider, tableSchemas);
    }

    public async Task<PreviewResult> ExecutePreviewAsync(
        string sql, int maxRows = 200, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);

            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText  = WrapWithLimit(sql, maxRows);
                cmd.CommandTimeout = Config.TimeoutSeconds;

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                var dt = new DataTable();
                dt.Load(reader);

                sw.Stop();
                return new PreviewResult(true, dt,
                    ExecutionTime: sw.Elapsed,
                    RowsAffected: dt.Rows.Count);
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
    /// Wraps the raw SQL in a provider-specific SELECT ... LIMIT/TOP clause
    /// so preview results are always capped at <paramref name="maxRows"/>.
    /// </summary>
    protected virtual string WrapWithLimit(string sql, int maxRows) =>
        $"SELECT * FROM ({sql}) AS __preview LIMIT {maxRows}";

    // ── Disposal ──────────────────────────────────────────────────────────────
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;
}
