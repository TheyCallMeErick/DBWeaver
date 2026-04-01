using System.Data;

namespace VisualSqlArchitect.Core;

// ─── Schema Models ────────────────────────────────────────────────────────────

public record ColumnSchema(
    string Name,
    string DataType,
    bool IsNullable,
    bool IsPrimaryKey,
    bool IsForeignKey,
    string? ForeignKeyTable = null,
    int? MaxLength = null
);

public record TableSchema(string Schema, string Name, IReadOnlyList<ColumnSchema> Columns)
{
    public string FullName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
}

public record DatabaseSchema(
    string DatabaseName,
    DatabaseProvider Provider,
    IReadOnlyList<TableSchema> Tables
);

// ─── Connection / Execution Models ───────────────────────────────────────────

public record ConnectionConfig(
    DatabaseProvider Provider,
    string Host,
    int Port,
    string Database,
    string Username,
    string Password,
    bool UseIntegratedSecurity = false,
    int TimeoutSeconds = 30,
    IDictionary<string, string>? ExtraParameters = null
)
{
    public string BuildConnectionString() =>
        Provider switch
        {
            DatabaseProvider.SqlServer => BuildSqlServerCs(),
            DatabaseProvider.MySql => BuildMySqlCs(),
            DatabaseProvider.Postgres => BuildPostgresCs(),
            DatabaseProvider.SQLite => BuildSqliteCs(),
            _ => throw new NotSupportedException($"Provider {Provider} is not supported."),
        };

    private string BuildSqlServerCs()
    {
        string auth = UseIntegratedSecurity
            ? "Integrated Security=True"
            : $"User Id={Username};Password={Password}";
        return $"Server={Host},{Port};Database={Database};{auth};TrustServerCertificate=True;Connection Timeout={TimeoutSeconds};";
    }

    private string BuildMySqlCs() =>
        $"Server={Host};Port={Port};Database={Database};Uid={Username};Pwd={Password};ConnectionTimeout={TimeoutSeconds};";

    private string BuildPostgresCs() =>
        $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};Timeout={TimeoutSeconds};";

    private string BuildSqliteCs() =>
        $"Data Source={Database};Default Timeout={TimeoutSeconds};";
}

public record ConnectionTestResult(
    bool Success,
    string? ErrorMessage = null,
    TimeSpan? Latency = null
);

public record PreviewResult(
    bool Success,
    DataTable? Data = null,
    string? ErrorMessage = null,
    TimeSpan? ExecutionTime = null,
    long? RowsAffected = null
);

// ─── Provider Enum ────────────────────────────────────────────────────────────

public enum DatabaseProvider
{
    SqlServer,
    MySql,
    Postgres,
    SQLite,
}

// ─── Core Orchestrator Interface ──────────────────────────────────────────────

/// <summary>
/// Central contract for all database operations in Visual SQL Architect.
/// Each provider implements this interface, keeping the canvas nodes
/// completely agnostic of the underlying database engine.
/// </summary>
public interface IDbOrchestrator : IAsyncDisposable
{
    DatabaseProvider Provider { get; }
    ConnectionConfig Config { get; }

    /// <summary>Validates the connection and returns latency metrics.</summary>
    Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default);

    /// <summary>
    /// Introspects the database and returns a full schema snapshot
    /// including tables, columns, PKs and FK relationships.
    /// </summary>
    Task<DatabaseSchema> GetSchemaAsync(CancellationToken ct = default);

    /// <summary>
    /// Executes a read-only query preview, capped at <paramref name="maxRows"/>.
    /// Wraps the query in a transaction that is always rolled back.
    /// </summary>
    Task<PreviewResult> ExecutePreviewAsync(
        string sql,
        int maxRows = 200,
        CancellationToken ct = default
    );
}
