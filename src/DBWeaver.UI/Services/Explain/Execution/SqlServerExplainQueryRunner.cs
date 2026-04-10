using Microsoft.Data.SqlClient;
using DBWeaver.UI.ViewModels.Canvas;
using System.Data.SqlTypes;
using System.Text;
using DBWeaver.Core;
using System.Diagnostics.CodeAnalysis;

namespace DBWeaver.UI.Services.Explain;

[ExcludeFromCodeCoverage]
public sealed class SqlServerExplainQueryRunner : ISqlServerExplainQueryRunner
{
    public async Task<string> ExecuteShowPlanXmlAsync(
        string sql,
        ConnectionConfig connectionConfig,
        CancellationToken ct = default
    )
    {
        return await ExecuteWithSessionOptionAsync(
            sql,
            connectionConfig,
            setOn: "SET SHOWPLAN_XML ON;",
            setOff: "SET SHOWPLAN_XML OFF;",
            ct: ct
        );
    }

    public async Task<string> ExecuteStatisticsXmlAsync(
        string sql,
        ConnectionConfig connectionConfig,
        CancellationToken ct = default
    )
    {
        return await ExecuteWithSessionOptionAsync(
            sql,
            connectionConfig,
            setOn: "SET STATISTICS XML ON;",
            setOff: "SET STATISTICS XML OFF;",
            ct: ct
        );
    }

    private static async Task<string> ExecuteWithSessionOptionAsync(
        string sql,
        ConnectionConfig connectionConfig,
        string setOn,
        string setOff,
        CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionConfig.BuildConnectionString());
        await conn.OpenAsync(ct);

        await SetSessionOptionAsync(conn, setOn, connectionConfig.TimeoutSeconds, ct);

        try
        {
            await using var queryCmd = conn.CreateCommand();
            queryCmd.CommandTimeout = connectionConfig.TimeoutSeconds;
            queryCmd.CommandText = sql;
            await using var reader = await queryCmd.ExecuteReaderAsync(ct);
            return await ReadFirstShowPlanXmlAsync(reader, ct);
        }
        finally
        {
            await SetSessionOptionAsync(conn, setOff, connectionConfig.TimeoutSeconds, ct);
        }
    }

    private static async Task<string> ReadFirstShowPlanXmlAsync(SqlDataReader reader, CancellationToken ct)
    {
        string? firstNonEmptyPayload = null;

        do
        {
            while (await reader.ReadAsync(ct))
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.IsDBNull(i))
                        continue;

                    string value = ConvertToString(reader.GetValue(i));
                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    string normalized = NormalizeXmlPayload(value);
                    firstNonEmptyPayload ??= normalized;
                    if (LooksLikeXmlPlan(normalized))
                        return normalized;
                }
            }
        }
        while (await reader.NextResultAsync(ct));

        return NormalizeXmlPayload(firstNonEmptyPayload ?? string.Empty);
    }

    private static async Task SetSessionOptionAsync(
        SqlConnection conn,
        string commandText,
        int timeoutSeconds,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = timeoutSeconds;
        cmd.CommandText = commandText;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string ConvertToString(object value)
    {
        return value switch
        {
            SqlXml sqlXml => sqlXml.Value,
            byte[] bytes => DecodeBytes(bytes),
            string text => text,
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static bool LooksLikeXmlPlan(string value)
    {
        string trimmed = value.TrimStart();
        return trimmed.Contains("<ShowPlanXML", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("<", StringComparison.Ordinal);
    }

    private static string DecodeBytes(byte[] bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        string unicode = Encoding.Unicode.GetString(bytes).Trim('\0');
        if (LooksLikeXmlPlan(unicode))
            return unicode;

        string utf8 = Encoding.UTF8.GetString(bytes).Trim('\0');
        return utf8;
    }

    private static string NormalizeXmlPayload(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string trimmed = value.Trim();

        int start = trimmed.IndexOf("<ShowPlanXML", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            start = trimmed.IndexOf("<?xml", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            start = trimmed.IndexOf('<');

        return start > 0 ? trimmed[start..] : trimmed;
    }
}



