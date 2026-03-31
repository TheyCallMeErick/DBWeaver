using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using VisualSqlArchitect.Providers.Dialects;
using VisualSqlArchitect.Metadata;

namespace VisualSqlArchitect.Providers;

/// <summary>
/// SQLite implementation of IDbOrchestrator.
/// Works with SQLite 3.0+, using sqlite_master system tables for metadata.
/// </summary>
public sealed class SqliteOrchestrator(Core.ConnectionConfig config)
    : Core.BaseDbOrchestrator(config)
{
    public override Core.DatabaseProvider Provider => Core.DatabaseProvider.SQLite;

    // ── Dialect ───────────────────────────────────────────────────────────────
    protected override Dialects.ISqlDialect GetDialect() => new SqliteDialect();

    // ── Metadata Query Provider ───────────────────────────────────────────────
    protected override IMetadataQueryProvider GetMetadataQueryProvider() => new SqliteMetadataQueries();

    // ── Connection ────────────────────────────────────────────────────────────
    protected override async Task<DbConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var conn = new SqliteConnection(Config.BuildConnectionString());
        await conn.OpenAsync(ct);

        // Enable foreign keys support by default
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys = ON;";
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return conn;
    }
}
