
namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryPreviewSqlPreviewFormatter(DatabaseProvider provider)
{
    private readonly DatabaseProvider _provider = provider;

    public string InlineBindingsForPreview(string sql, IReadOnlyDictionary<string, object?> bindings)
    {
        if (string.IsNullOrWhiteSpace(sql) || bindings.Count == 0)
            return sql;

        string inlinedSql = sql;

        foreach ((string key, object? value) in bindings.OrderByDescending(k => k.Key.Length))
        {
            string placeholder = key.StartsWith("@", StringComparison.Ordinal)
                || key.StartsWith(":", StringComparison.Ordinal)
                ? key
                : "@" + key;

            string literal = ToSqlLiteral(value);
            string escaped = Regex.Escape(placeholder);
            inlinedSql = Regex.Replace(inlinedSql, $@"(?<![A-Za-z0-9_]){escaped}(?![A-Za-z0-9_])", literal);

            if (!placeholder.StartsWith(":", StringComparison.Ordinal))
            {
                string colonPlaceholder = ":" + placeholder.TrimStart('@');
                string colonEscaped = Regex.Escape(colonPlaceholder);
                inlinedSql = Regex.Replace(inlinedSql, $@"(?<![A-Za-z0-9_]){colonEscaped}(?![A-Za-z0-9_])", literal);
            }
        }

        return inlinedSql;
    }

    private string ToSqlLiteral(object? value)
    {
        if (value is null || value == DBNull.Value)
            return "NULL";

        if (value is bool boolValue)
        {
            return _provider == DatabaseProvider.SqlServer
                ? (boolValue ? "1" : "0")
                : (boolValue ? "TRUE" : "FALSE");
        }

        if (value is string stringValue)
            return "'" + stringValue.Replace("'", "''", StringComparison.Ordinal) + "'";

        if (value is DateTime dateTime)
            return "'" + dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "'";

        if (value is DateTimeOffset dateTimeOffset)
            return "'" + dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture) + "'";

        if (value is Guid guid)
            return "'" + guid.ToString("D", CultureInfo.InvariantCulture) + "'";

        if (value is byte[] bytes)
            return "0x" + Convert.ToHexString(bytes);

        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture) ?? "NULL";

        return "'" + value.ToString()?.Replace("'", "''", StringComparison.Ordinal) + "'";
    }
}



