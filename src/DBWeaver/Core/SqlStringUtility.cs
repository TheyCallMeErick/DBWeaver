namespace DBWeaver.Core;

internal static class SqlStringUtility
{
    internal static string QuoteLiteral(string value) =>
        $"'{value.Replace("'", "''")}'";
}
