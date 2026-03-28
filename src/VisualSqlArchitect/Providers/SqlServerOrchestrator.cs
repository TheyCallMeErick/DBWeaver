using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using VisualSqlArchitect.Providers.Dialects;
using VisualSqlArchitect.Metadata;

namespace VisualSqlArchitect.Providers;

/// <summary>
/// SQL Server implementation of IDbOrchestrator.
/// Schema discovery relies on INFORMATION_SCHEMA views for broad compatibility
/// (SQL Server 2012+ / Azure SQL).
/// </summary>
public sealed class SqlServerOrchestrator(Core.ConnectionConfig config)
    : Core.BaseDbOrchestrator(config)
{
    public override Core.DatabaseProvider Provider => Core.DatabaseProvider.SqlServer;

    // ── Dialect ───────────────────────────────────────────────────────────────
    protected override Dialects.ISqlDialect GetDialect() => new SqlServerDialect();

    // ── Metadata Query Provider ───────────────────────────────────────────────
    protected override IMetadataQueryProvider GetMetadataQueryProvider() => new SqlServerMetadataQueries();

    // ── Connection ────────────────────────────────────────────────────────────
    protected override async Task<DbConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var conn = new SqlConnection(Config.BuildConnectionString());
        await conn.OpenAsync(ct);
        return conn;
    }
}
