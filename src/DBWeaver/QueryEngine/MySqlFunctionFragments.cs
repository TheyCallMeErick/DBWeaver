namespace DBWeaver.QueryEngine;

public sealed class MySqlFunctionFragments : IFunctionFragmentProvider
{
    public string Regex(string column, string pattern) =>
        column + " REGEXP " + pattern;

    public string JsonExtract(string column, string jsonPath) =>
        "JSON_EXTRACT(" + column + ", '$." + jsonPath + "')";

    public string DateDiff(string fromExpr, string toExpr, string unit) =>
        "TIMESTAMPDIFF(" + unit + ", " + fromExpr + ", " + toExpr + ")";

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
            string s => "'" + s.Replace("'", "\\'") + "'",
            null => "NULL",
            _ => value.ToString() ?? "NULL"
        };
}
