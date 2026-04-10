namespace DBWeaver.QueryEngine;

public sealed class SqlServerFunctionFragments : IFunctionFragmentProvider
{
    public string Regex(string column, string pattern) =>
        "PATINDEX(" + pattern + ", " + column + ") > 0";

    public string JsonExtract(string column, string jsonPath) =>
        "JSON_VALUE(" + column + ", '$." + jsonPath + "')";

    public string DateDiff(string fromExpr, string toExpr, string unit) =>
        "DATEDIFF(" + unit + ", " + fromExpr + ", " + toExpr + ")";

    public string Concat(params string[] expressions) =>
        "CONCAT(" + string.Join(", ", expressions) + ")";

    public string Like(string column, string pattern) =>
        column + " LIKE " + pattern;

    public string NotLike(string column, string pattern) =>
        column + " NOT LIKE " + pattern;

    public string In(string column, params object[] values)
    {
        var formatted = string.Join(", ", values.Select(FormatValue));
        return column + " IN (" + formatted + ")";
    }

    public string NotIn(string column, params object[] values)
    {
        var formatted = string.Join(", ", values.Select(FormatValue));
        return column + " NOT IN (" + formatted + ")";
    }

    private static string FormatValue(object value) =>
        value switch
        {
            string s => "'" + s.Replace("'", "''") + "'",
            null => "NULL",
            _ => value.ToString() ?? "NULL"
        };
}
