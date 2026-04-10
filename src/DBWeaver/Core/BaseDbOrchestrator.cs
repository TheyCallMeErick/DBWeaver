using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using DBWeaver.Providers.Dialects;
using DBWeaver.Metadata;

namespace DBWeaver.Core;

/// <summary>
/// Provides the common scaffolding (timing, preview capping, safe disposal)
/// shared across all provider implementations.
/// Concrete classes only need to supply: a live connection, and provider-specific
/// schema queries via <see cref="FetchTablesAsync"/> / <see cref="FetchColumnsAsync"/>.
/// </summary>
public abstract class BaseDbOrchestrator(
    ConnectionConfig config,
    ILogger<BaseDbOrchestrator>? logger = null,
    IOptions<PreviewExecutionOptions>? previewOptions = null
) : IDbOrchestrator
{
    private bool _disposed;
    private readonly ILogger<BaseDbOrchestrator> _logger = logger ?? NullLogger<BaseDbOrchestrator>.Instance;
    private readonly int _defaultPreviewMaxRows = PreviewExecutionOptions.ResolveDefaultMaxRows(previewOptions);

    /// <summary>
    /// Gets the provider kind for the concrete orchestrator implementation.
    /// </summary>
    public abstract DatabaseProvider Provider { get; }

    /// <summary>
    /// Gets the immutable connection configuration used by this orchestrator.
    /// </summary>
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
            _logger.LogWarning(ex, "Connection test failed for provider {Provider}.", Provider);
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
        int maxRows = PreviewExecutionOptions.UseConfiguredDefault,
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
                int resolvedMaxRows = maxRows > 0 ? maxRows : _defaultPreviewMaxRows;
                cmd.CommandText = GetDialect().WrapWithPreviewLimit(sql, resolvedMaxRows);
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
            _logger.LogWarning(ex, "Preview execution failed for provider {Provider}.", Provider);
            return new PreviewResult(false, ErrorMessage: ex.Message, ExecutionTime: sw.Elapsed);
        }
    }

    public async Task<DdlExecutionResult> ExecuteDdlAsync(
        string sql,
        bool stopOnError = true,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("DDL script cannot be empty.", nameof(sql));

        var statements = SqlStatementSplitter.Split(sql);
        if (statements.Count == 0)
            throw new ArgumentException("DDL script does not contain executable statements.", nameof(sql));

        var sw = Stopwatch.StartNew();
        var results = new List<DdlStatementExecutionResult>(statements.Count);

        await using DbConnection conn = await OpenConnectionAsync(ct);
        for (int i = 0; i < statements.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            string statement = statements[i];
            try
            {
                await using DbCommand cmd = conn.CreateCommand();
                cmd.CommandText = statement;
                cmd.CommandTimeout = Config.TimeoutSeconds;

                int affected = await cmd.ExecuteNonQueryAsync(ct);
                results.Add(
                    new DdlStatementExecutionResult(
                        i + 1,
                        statement,
                        Success: true,
                        RowsAffected: affected
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "DDL statement execution failed for provider {Provider} at index {StatementIndex}.",
                    Provider,
                    i + 1
                );
                results.Add(
                    new DdlStatementExecutionResult(
                        i + 1,
                        statement,
                        Success: false,
                        ErrorMessage: ex.Message
                    )
                );

                if (stopOnError)
                    break;
            }
        }

        sw.Stop();
        bool success = results.All(r => r.Success);
        return new DdlExecutionResult(success, results, sw.Elapsed);
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
