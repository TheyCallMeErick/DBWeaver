using Microsoft.Data.Sqlite;
using DBWeaver.UI.ViewModels.Canvas;
using DBWeaver.Core;

namespace DBWeaver.UI.Services.Explain;

public sealed class SqliteExplainExecutor : IExplainExecutor
{
    public async Task<ExplainResult> RunAsync(
        string sql,
        DatabaseProvider provider,
        ConnectionConfig? connectionConfig,
        ExplainOptions options,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        if (provider != DatabaseProvider.SQLite)
            throw new ArgumentException("Provider must be SQLite.", nameof(provider));
        if (connectionConfig is null || connectionConfig.Provider != DatabaseProvider.SQLite)
            throw new ArgumentException("A valid SQLite connection config is required.", nameof(connectionConfig));

        await using var conn = new SqliteConnection(connectionConfig.BuildConnectionString());
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = connectionConfig.TimeoutSeconds;
        cmd.CommandText = $"EXPLAIN QUERY PLAN {sql}";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<SqliteExplainRow>();

        while (await reader.ReadAsync(ct))
        {
            rows.Add(
                new SqliteExplainRow(
                    Id: reader.GetInt32(0),
                    Parent: reader.GetInt32(1),
                    Detail: reader.GetString(3)
                )
            );
        }

        IReadOnlyList<ExplainNode> nodes = MapRows(rows);
        string raw = string.Join(Environment.NewLine, rows.Select(r => $"{r.Id}|{r.Parent}|{r.Detail}"));
        return new ExplainResult(nodes, null, null, raw, IsSimulated: false);
    }

    private static IReadOnlyList<ExplainNode> MapRows(IReadOnlyList<SqliteExplainRow> rows)
    {
        if (rows.Count == 0)
            return [];

        Dictionary<int, SqliteExplainRow> byId = rows.ToDictionary(r => r.Id, r => r);
        var nodes = new List<ExplainNode>(rows.Count);

        for (int i = 0; i < rows.Count; i++)
        {
            SqliteExplainRow row = rows[i];
            int indent = ResolveIndentLevel(row, byId);
            string alert = ResolveAlertLabel(row.Detail);

            nodes.Add(
                new ExplainNode
                {
                    NodeType = ResolveOperation(row.Detail),
                    Detail = row.Detail,
                    IndentLevel = indent,
                    IsExpensive = alert.Length > 0,
                    AlertLabel = alert,
                }
            );
        }

        return nodes;
    }

    private static int ResolveIndentLevel(
        SqliteExplainRow row,
        IReadOnlyDictionary<int, SqliteExplainRow> byId
    )
    {
        int depth = 0;
        int parent = row.Parent;
        var seen = new HashSet<int>();

        while (parent >= 0 && byId.TryGetValue(parent, out SqliteExplainRow? p) && seen.Add(parent))
        {
            depth++;
            parent = p.Parent;
        }

        return depth;
    }

    private static string ResolveOperation(string detail)
    {
        if (detail.StartsWith("SEARCH ", StringComparison.OrdinalIgnoreCase))
            return "Index/Search";

        if (detail.StartsWith("SCAN ", StringComparison.OrdinalIgnoreCase))
            return "Table Scan";

        if (detail.Contains("USE TEMP B-TREE", StringComparison.OrdinalIgnoreCase))
            return "Temp B-Tree";

        return "Plan Step";
    }

    private static string ResolveAlertLabel(string detail)
    {
        if (detail.Contains("USE TEMP B-TREE", StringComparison.OrdinalIgnoreCase))
            return "SORT";

        bool isScan = detail.StartsWith("SCAN ", StringComparison.OrdinalIgnoreCase);
        bool hasIndex = detail.Contains("USING INDEX", StringComparison.OrdinalIgnoreCase);
        if (isScan && !hasIndex)
            return "SEQ SCAN";

        return string.Empty;
    }

    private sealed record SqliteExplainRow(int Id, int Parent, string Detail);
}



