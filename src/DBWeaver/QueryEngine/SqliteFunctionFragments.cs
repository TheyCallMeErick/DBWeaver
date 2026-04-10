namespace DBWeaver.QueryEngine;

/// <summary>
/// SQLite implementation of IFunctionFragmentProvider.
/// SQLite has limited advanced functions; many operations use workarounds.
/// </summary>
public sealed class SqliteFunctionFragments : IFunctionFragmentProvider
{
    public string Regex(string column, string pattern)
    {
        // SQLite doesn't have native regex support by default
        // Use GLOB for shell-style patterns or use json1 extension if available
        // For now, we'll use a simple LIKE approximation or require client-side regex
        return $"{column} GLOB {pattern}";
    }

    public string JsonExtract(string column, string jsonPath)
    {
        // SQLite json1 extension: json_extract(col, path)
        // Returns JSON text; use json_unquote for scalar values
        return $"json_unquote(json_extract({column}, {jsonPath}))";
    }

    public string DateDiff(string fromExpr, string toExpr, string unit)
    {
        // SQLite uses julianday() and date manipulation functions
        // Simple day difference between dates
        return $"julianday({toExpr}) - julianday({fromExpr})";
    }

    public string Concat(params string[] expressions)
    {
        // SQLite uses || for concatenation
        return string.Join(" || ", expressions);
    }

    public string Like(string column, string pattern) =>
        $"{column} LIKE {pattern}";

    public string NotLike(string column, string pattern) =>
        $"{column} NOT LIKE {pattern}";

    public string In(string column, params object[] values)
    {
        var formatted = string.Join(", ", values.Select(FormatValue));
        return $"{column} IN ({formatted})";
    }

    public string NotIn(string column, params object[] values)
    {
        var formatted = string.Join(", ", values.Select(FormatValue));
        return $"{column} NOT IN ({formatted})";
    }

    private static string FormatValue(object value) =>
        value switch
        {
            string s => "'" + s.Replace("'", "''") + "'",
            null => "NULL",
            _ => value.ToString() ?? "NULL"
        };
}
