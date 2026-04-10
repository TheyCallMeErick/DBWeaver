namespace DBWeaver.QueryEngine;

/// <summary>
/// Abstraction over query building libraries (SqlKata, LinqToDb, etc).
/// Enables switching implementations without affecting consumers,
/// and facilitates testing without vendor-specific dependencies.
///
/// Clients call builder methods fluently, then invoke Compile() for final SQL.
/// </summary>
public interface IQueryBuilder
{
    /// <summary>
    /// Add SELECT columns to the query. If empty, defaults to SELECT *.
    /// </summary>
    IQueryBuilder Select(IEnumerable<SelectColumn> columns);

    /// <summary>
    /// Add JOIN clauses to the query.
    /// </summary>
    IQueryBuilder Join(IEnumerable<JoinDefinition> joins);

    /// <summary>
    /// Add WHERE filter clauses to the query.
    /// </summary>
    IQueryBuilder Filter(IEnumerable<FilterDefinition> filters);

    /// <summary>
    /// Add GROUP BY clauses to the query.
    /// </summary>
    IQueryBuilder GroupBy(IEnumerable<string> columns);

    /// <summary>
    /// Add ORDER BY clauses to the query.
    /// </summary>
    IQueryBuilder OrderBy(IEnumerable<OrderDefinition> orders);

    /// <summary>
    /// Add LIMIT / OFFSET pagination to the query.
    /// </summary>
    IQueryBuilder Limit(int? limit, int? offset);

    /// <summary>
    /// Compile the query into SQL and parameter bindings.
    /// </summary>
    CompiledQuery Compile();
}
