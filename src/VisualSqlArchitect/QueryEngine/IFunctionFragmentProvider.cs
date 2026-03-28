namespace VisualSqlArchitect.QueryEngine;

/// <summary>
/// Abstraction for provider-specific SQL function fragment generation.
/// Each database has different syntax for regex, JSON, date operations, etc.
/// </summary>
public interface IFunctionFragmentProvider
{
    /// <summary>
    /// Generate REGEX comparison fragment: column ~ pattern (Postgres) vs REGEXP (MySQL) vs PATINDEX (SQL Server)
    /// </summary>
    string Regex(string column, string pattern);

    /// <summary>
    /// Generate JSON extraction fragment: column->>'key' (Postgres) vs JSON_EXTRACT (MySQL/SQL Server)
    /// </summary>
    string JsonExtract(string column, string jsonPath);

    /// <summary>
    /// Generate date difference calculation: column::date (Postgres) vs CAST (MySQL/SQL Server)
    /// </summary>
    string DateDiff(string fromExpr, string toExpr, string unit);

    /// <summary>
    /// Generate string concatenation fragment: || (Postgres) vs + or CONCAT (MySQL/SQL Server)
    /// </summary>
    string Concat(params string[] expressions);

    /// <summary>
    /// Generate LIKE expression with escaping.
    /// </summary>
    string Like(string column, string pattern);

    /// <summary>
    /// Generate NOT LIKE expression.
    /// </summary>
    string NotLike(string column, string pattern);

    /// <summary>
    /// Generate IN expression with array/list values.
    /// </summary>
    string In(string column, params object[] values);

    /// <summary>
    /// Generate NOT IN expression with array/list values.
    /// </summary>
    string NotIn(string column, params object[] values);
}
