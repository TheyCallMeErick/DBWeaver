using System.Data;
using System.Data.Common;
using Npgsql;
using DBWeaver.Providers.Dialects;
using DBWeaver.Metadata;

namespace DBWeaver.Providers;

/// <summary>
/// PostgreSQL implementation of IDbOrchestrator.
/// Uses pg_catalog system tables for richer metadata than INFORMATION_SCHEMA.
/// Compatible with Postgres 12+.
/// </summary>
public sealed class PostgresOrchestrator(Core.ConnectionConfig config)
    : Core.BaseDbOrchestrator(config)
{
    public override Core.DatabaseProvider Provider => Core.DatabaseProvider.Postgres;

    // ── Dialect ───────────────────────────────────────────────────────────────
    protected override Dialects.ISqlDialect GetDialect() => new PostgresDialect();

    // ── Metadata Query Provider ───────────────────────────────────────────────
    protected override IMetadataQueryProvider GetMetadataQueryProvider() => new PostgresMetadataQueries();

    // ── Connection ────────────────────────────────────────────────────────────
    protected override async Task<DbConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var conn = new NpgsqlConnection(Config.BuildConnectionString());
        await conn.OpenAsync(ct);
        return conn;
    }
}
