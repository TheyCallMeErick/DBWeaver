using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Providers;

namespace VisualSqlArchitect.UI.Services;

/// <summary>
/// Executes SQL queries against a connected database and returns results as DataTable.
/// Handles connection management, timeouts, and safe preview mode execution.
/// </summary>
public sealed partial class QueryExecutorService
{
    private readonly ILogger<QueryExecutorService> _logger;

    public QueryExecutorService(ILogger<QueryExecutorService>? logger = null)
    {
        _logger = logger ?? NullLogger<QueryExecutorService>.Instance;
    }

    /// <summary>
    /// Executes a SQL query against the database and returns results.
    /// </summary>
    /// <param name="config">Database connection configuration</param>
    /// <param name="query">SQL query to execute</param>
    /// <param name="maxRows">Maximum number of rows to return (default 1000)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>DataTable containing query results</returns>
    public async Task<DataTable> ExecuteQueryAsync(
        ConnectionConfig config,
        string query,
        int maxRows = 1000,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "ExecuteQueryAsync called with config={Config}, query length={QueryLength}",
            config,
            query?.Length
        );

        if (config == null)
        {
            _logger.LogWarning("Connection config is null, returning demo data");
            return BuildDemoDataTable();
        }
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        try
        {
            _logger.LogInformation("Starting query execution on {Provider} ({Host}:{Port}/{Database})",
                config.Provider, config.Host, config.Port, config.Database);

            ValidatePreviewQuery(query);

            // Wrap query with LIMIT/TOP clause for safe preview
            string wrappedQuery = WrapWithPreviewLimit(query, config.Provider, maxRows);
            _logger.LogDebug("Wrapped query: {Query}", wrappedQuery);

            // Create orchestrator for the database provider
            var orchestrator = CreateOrchestrator(config.Provider, config);
            _logger.LogInformation("Created orchestrator for {Provider}", config.Provider);

            var result = await ExecuteQueryInternalAsync(orchestrator, wrappedQuery, ct);

            _logger.LogInformation("Query executed successfully. Rows: {RowCount}", result.Rows.Count);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Query execution was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute query: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Wraps a query with a LIMIT/TOP clause based on the database provider.
    /// This ensures preview queries don't return excessive data.
    /// </summary>
    private static string WrapWithPreviewLimit(string query, DatabaseProvider provider, int maxRows)
    {
        int boundedMaxRows = Math.Clamp(maxRows, 1, 10_000);

        // Remove trailing semicolon if present
        query = query.TrimEnd().TrimEnd(';');

        return provider switch
        {
            DatabaseProvider.SqlServer =>
                $"SELECT TOP {boundedMaxRows} * FROM ({query}) AS __preview",
            DatabaseProvider.MySql =>
                $"SELECT * FROM ({query}) AS __preview LIMIT {boundedMaxRows}",
            DatabaseProvider.Postgres =>
                $"SELECT * FROM ({query}) AS __preview LIMIT {boundedMaxRows}",
            DatabaseProvider.SQLite =>
                $"SELECT * FROM ({query}) AS __preview LIMIT {boundedMaxRows}",
            _ => throw new NotSupportedException($"Provider {provider} is not supported")
        };
    }

    private static void ValidatePreviewQuery(string query)
    {
        string trimmed = query.Trim();

        // Reject multiple statements to avoid stacked execution.
        int semicolon = trimmed.IndexOf(';');
        if (semicolon >= 0)
        {
            string trailing = trimmed[(semicolon + 1)..].Trim();
            if (trailing.Length > 0)
                throw new ArgumentException("Preview accepts a single SQL statement only.", nameof(query));
        }

        string firstToken = Regex.Match(trimmed, @"^\s*(\w+)", RegexOptions.CultureInvariant)
            .Groups[1]
            .Value
            .ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(firstToken))
            throw new ArgumentException("Query cannot be empty.", nameof(query));

        // Preview mode must remain read-only.
        if (firstToken is "INSERT" or "UPDATE" or "DELETE" or "MERGE" or "TRUNCATE" or
            "DROP" or "ALTER" or "CREATE" or "REPLACE" or "GRANT" or "REVOKE" or "CALL" or "EXEC")
            throw new ArgumentException("Preview mode only supports read-only SQL statements.", nameof(query));

        ValidateParameterBoundaries(trimmed, query);
    }

    private static void ValidateParameterBoundaries(string trimmedQuery, string originalQuery)
    {
        // Preview path executes SQL text directly and does not bind parameters.
        // Reject placeholders to avoid accidental execution assumptions.
        if (NamedSqlParameterRegex().IsMatch(trimmedQuery))
            throw new ArgumentException(
                "Preview mode does not support bound parameters in execution SQL. Inline safe literals or run the query outside preview.",
                nameof(originalQuery)
            );

        if (PositionalSqlParameterRegex().IsMatch(trimmedQuery))
            throw new ArgumentException(
                "Preview mode does not support positional parameter placeholders ('?' or '$1').",
                nameof(originalQuery)
            );
    }

    [GeneratedRegex(@"(?<!@)@[A-Za-z_][A-Za-z0-9_]*|(?<!:):[A-Za-z_][A-Za-z0-9_]*", RegexOptions.CultureInvariant)]
    private static partial Regex NamedSqlParameterRegex();

    [GeneratedRegex(@"\?|\$\d+", RegexOptions.CultureInvariant)]
    private static partial Regex PositionalSqlParameterRegex();

    /// <summary>
    /// Creates the appropriate orchestrator for the given provider.
    /// </summary>
    private IDbOrchestrator CreateOrchestrator(DatabaseProvider provider, ConnectionConfig config)
    {
        _logger.LogDebug("Creating orchestrator for provider: {Provider}", provider);
        return provider switch
        {
            DatabaseProvider.SqlServer => new SqlServerOrchestrator(config),
            DatabaseProvider.MySql => new MySqlOrchestrator(config),
            DatabaseProvider.Postgres => new PostgresOrchestrator(config),
            DatabaseProvider.SQLite => new SqliteOrchestrator(config),
            _ => throw new NotSupportedException($"Provider {provider} is not supported")
        };
    }

    /// <summary>
    /// Returns demo data for testing when real connection is not available.
    /// </summary>
    private DataTable BuildDemoDataTable()
    {
        var dt = new DataTable("demo_results");
        dt.Columns.Add("id", typeof(int));
        dt.Columns.Add("name", typeof(string));
        dt.Columns.Add("value", typeof(decimal));
        dt.Columns.Add("created_at", typeof(DateTime));

        var rng = new Random(42);
        for (int i = 1; i <= 10; i++)
        {
            dt.Rows.Add(
                i,
                $"Record {i}",
                Math.Round(rng.NextDouble() * 1000, 2),
                DateTime.Now.AddDays(-rng.Next(0, 30))
            );
        }

        _logger.LogDebug("Demo DataTable created with {RowCount} rows", dt.Rows.Count);
        return dt;
    }

    /// <summary>
    /// Executes a query using direct connection from the orchestrator.
    /// </summary>
    private async Task<DataTable> ExecuteQueryInternalAsync(
        IDbOrchestrator orchestrator,
        string query,
        CancellationToken ct)
    {
        var dt = new DataTable();

        try
        {
            // Use reflection to access OpenConnectionAsync since it's protected
            var method = orchestrator.GetType().GetMethod(
                "OpenConnectionAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method == null)
                throw new InvalidOperationException("Cannot find OpenConnectionAsync method on orchestrator");

            var task = method.Invoke(orchestrator, new object[] { ct }) as Task<DbConnection>;
            if (task == null)
                throw new InvalidOperationException("Failed to invoke OpenConnectionAsync");

            _logger.LogDebug("Opening database connection");
            await using (DbConnection conn = await task)
            {
                _logger.LogDebug("Connection opened, executing query");

                using (var command = conn.CreateCommand())
                {
                    command.CommandText = query;
                    command.CommandTimeout = 300; // 5 minutes default timeout

                    _logger.LogDebug("Executing command: {Query}", query);
                    using (var reader = await command.ExecuteReaderAsync(ct))
                    {
                        _logger.LogDebug("Reader obtained, loading data into DataTable");
                        dt.Load(reader);
                        _logger.LogInformation("DataTable loaded with {RowCount} rows and {ColumnCount} columns",
                            dt.Rows.Count, dt.Columns.Count);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during query execution: {Message}", ex.Message);
            throw;
        }

        return dt;
    }
}
