using MySqlConnector;
using DBWeaver.UI.ViewModels.Canvas;
using DBWeaver.Core;
using System.Diagnostics.CodeAnalysis;

namespace DBWeaver.UI.Services.Explain;

[ExcludeFromCodeCoverage]
public sealed class MySqlExplainQueryRunner : IMySqlExplainQueryRunner
{
    public async Task<string> ExecuteFormatJsonAsync(
        string sql,
        ConnectionConfig connectionConfig,
        CancellationToken ct = default
    )
    {
        await using var conn = new MySqlConnection(connectionConfig.BuildConnectionString());
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = connectionConfig.TimeoutSeconds;
        cmd.CommandText = $"EXPLAIN FORMAT=JSON {sql}";

        object? payload = await cmd.ExecuteScalarAsync(ct);
        return payload?.ToString() ?? "{}";
    }

    public async Task<string> ExecuteAnalyzeAsync(
        string sql,
        ConnectionConfig connectionConfig,
        CancellationToken ct = default
    )
    {
        await using var conn = new MySqlConnection(connectionConfig.BuildConnectionString());
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = connectionConfig.TimeoutSeconds;
        cmd.CommandText = $"EXPLAIN ANALYZE {sql}";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var lines = new List<string>();
        while (await reader.ReadAsync(ct))
        {
            if (reader.FieldCount == 0 || reader.IsDBNull(0))
                continue;

            string? line = reader.GetValue(0)?.ToString();
            if (!string.IsNullOrWhiteSpace(line))
                lines.Add(line);
        }

        return string.Join(Environment.NewLine, lines);
    }
}



