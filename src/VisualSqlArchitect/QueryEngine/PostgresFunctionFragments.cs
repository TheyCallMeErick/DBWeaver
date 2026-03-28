namespace VisualSqlArchitect.QueryEngine;

public sealed class PostgresFunctionFragments : IFunctionFragmentProvider
{
    public string Regex(string column, string pattern) =>
        column + " ~ " + pattern;

    public string JsonExtract(string column, string jsonPath) =>
        column + "->>'" + jsonPath + "'";

    public string DateDiff(string fromExpr, string toExpr, string unit) =>
        "EXTRACT(" + unit + " FROM " + toExpr + " - " + fromExpr + ")";

    public string Concat(params string[] expressions) =>
        string.Join(" || ", expressions.Select(e => "CAST(" + e + " AS TEXT)"));

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
            string s => "'" + s + "'",
            null => "NULL",
            _ => value.ToString() ?? "NULL"
        };
}
