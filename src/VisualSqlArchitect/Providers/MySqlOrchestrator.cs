using System.Data;
using System.Data.Common;
using MySqlConnector;
using VisualSqlArchitect.Providers.Dialects;
using VisualSqlArchitect.Metadata;

namespace VisualSqlArchitect.Providers;

/// <summary>
/// MySQL / MariaDB implementation of IDbOrchestrator.
/// Uses INFORMATION_SCHEMA (MySQL 5.7+ / MariaDB 10.2+).
/// </summary>
public sealed class MySqlOrchestrator(Core.ConnectionConfig config)
    : Core.BaseDbOrchestrator(config)
{
    public override Core.DatabaseProvider Provider => Core.DatabaseProvider.MySql;

    // ── Dialect ───────────────────────────────────────────────────────────────
    protected override Dialects.ISqlDialect GetDialect() => new MySqlDialect();

    // ── Metadata Query Provider ───────────────────────────────────────────────
    protected override IMetadataQueryProvider GetMetadataQueryProvider() => new MySqlMetadataQueries();

    // ── Connection ────────────────────────────────────────────────────────────
    protected override async Task<DbConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var conn = new MySqlConnection(Config.BuildConnectionString());
        await conn.OpenAsync(ct);
        return conn;
    }
}
