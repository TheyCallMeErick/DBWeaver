using VisualSqlArchitect.Core;
using VisualSqlArchitect.Registry;

namespace VisualSqlArchitect.QueryEngine;

public record SelectColumn(string Expression, string? Alias = null);

public record JoinDefinition(
    string TargetTable,
    string LeftColumn,
    string RightColumn,
    string Type = "INNER",
    string Operator = "=",
    string? OnRaw = null
);

public record SetOperationDefinition(
    string Operator,
    string QuerySql
);

public record FilterDefinition(
    string Column,
    string Operator,
    object? Value,
    string? CanonicalFn = null,
    string[]? FnArgs = null
);

public record OrderDefinition(string Column, bool Descending = false);

public record VisualQuerySpec(
    string FromTable,
    IReadOnlyList<SelectColumn>? Selects = null,
    IReadOnlyList<JoinDefinition>? Joins = null,
    IReadOnlyList<FilterDefinition>? Filters = null,
    IReadOnlyList<OrderDefinition>? Orders = null,
    IReadOnlyList<string>? GroupBy = null,
    int? Limit = null,
    int? Offset = null
);

public record CompiledQuery(string Sql, IReadOnlyDictionary<string, object?> Bindings);

/// <summary>
/// Translates a <see cref="VisualQuerySpec"/> graph into SQL using <see cref="IQueryBuilder"/>.
/// Decoupled from SqlKata to enable testing and alternative query builders.
///
/// Lifecycle: one instance per active canvas connection.
/// </summary>
public sealed class QueryBuilderService(IQueryBuilder builder)
{
    private readonly IQueryBuilder _builder = builder;

    /// <summary>
    /// Factory: create with SqlKata default implementation.
    /// </summary>
    public static QueryBuilderService Create(DatabaseProvider provider, string fromTable) =>
        new(new SqlKataQueryBuilder(fromTable, provider, new SqlFunctionRegistry(provider)));

    /// <summary>
    /// Factory: create with custom function registry.
    /// </summary>
    public static QueryBuilderService Create(DatabaseProvider provider, string fromTable, ISqlFunctionRegistry registry) =>
        new(new SqlKataQueryBuilder(fromTable, provider, registry));

    /// <summary>
    /// Compile the visual query spec into SQL with parameter bindings.
    /// </summary>
    public CompiledQuery Compile(VisualQuerySpec spec) =>
        _builder
            .Select(spec.Selects ?? [])
            .Join(spec.Joins ?? [])
            .Filter(spec.Filters ?? [])
            .GroupBy(spec.GroupBy ?? [])
            .OrderBy(spec.Orders ?? [])
            .Limit(spec.Limit, spec.Offset)
            .Compile();
}

