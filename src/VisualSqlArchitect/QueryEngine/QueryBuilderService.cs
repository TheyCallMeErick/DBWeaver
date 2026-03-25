using SqlKata;
using SqlKata.Compilers;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Registry;

namespace VisualSqlArchitect.QueryEngine;

// ─── Query Node DTOs (canvas → engine) ───────────────────────────────────────

public record SelectColumn(string Expression, string? Alias = null);

public record JoinDefinition(
    string TargetTable,
    string LeftColumn,
    string RightColumn,
    string Type = "INNER"    // INNER | LEFT | RIGHT | CROSS
);

public record FilterDefinition(
    string Column,
    string Operator,         // =, !=, >, <, >=, <=, LIKE, IN, IS NULL, REGEX, …
    object? Value,
    string? CanonicalFn = null,   // if set, resolve via SqlFunctionRegistry
    string[]? FnArgs = null
);

public record OrderDefinition(string Column, bool Descending = false);

/// <summary>
/// Represents a complete visual query graph, serialised from canvas nodes.
/// </summary>
public record VisualQuerySpec(
    string FromTable,
    IReadOnlyList<SelectColumn>?   Selects  = null,
    IReadOnlyList<JoinDefinition>? Joins    = null,
    IReadOnlyList<FilterDefinition>? Filters = null,
    IReadOnlyList<OrderDefinition>? Orders  = null,
    IReadOnlyList<string>?         GroupBy  = null,
    int? Limit = null,
    int? Offset = null
);

// ─── Compiled result ──────────────────────────────────────────────────────────

public record CompiledQuery(string Sql, IReadOnlyDictionary<string, object?> Bindings);

// ─── Service ──────────────────────────────────────────────────────────────────

/// <summary>
/// Translates a <see cref="VisualQuerySpec"/> graph into a provider-correct SQL
/// string using SqlKata for structural clauses and <see cref="ISqlFunctionRegistry"/>
/// for function fragments that SqlKata cannot express natively.
///
/// Lifecycle: one instance per active canvas connection; replace when
/// the user switches providers.
/// </summary>
public sealed class QueryBuilderService
{
    private readonly Compiler _compiler;
    private readonly ISqlFunctionRegistry _fnRegistry;
    private readonly DatabaseProvider _provider;

    public QueryBuilderService(DatabaseProvider provider, ISqlFunctionRegistry fnRegistry)
    {
        _provider   = provider;
        _fnRegistry = fnRegistry;
        _compiler   = CreateCompiler(provider);
    }

    // ── Factory helper ────────────────────────────────────────────────────────

    /// <summary>
    /// Convenience factory — wires up the compiler and registry in one call.
    /// </summary>
    public static QueryBuilderService Create(DatabaseProvider provider) =>
        new(provider, new SqlFunctionRegistry(provider));

    // ── Main compilation entry point ──────────────────────────────────────────

    public CompiledQuery Compile(VisualQuerySpec spec)
    {
        var query = new Query(spec.FromTable);

        ApplySelects(query, spec.Selects);
        ApplyJoins(query, spec.Joins);
        ApplyFilters(query, spec.Filters);
        ApplyGroupBy(query, spec.GroupBy);
        ApplyOrders(query, spec.Orders);
        ApplyPagination(query, spec.Limit, spec.Offset);

        var result = _compiler.Compile(query);

        // SqlKata uses ? placeholders internally; map to named bindings
        var bindings = result.NamedBindings;

        return new CompiledQuery(result.Sql, bindings);
    }

    // ─── Clause builders ──────────────────────────────────────────────────────

    private void ApplySelects(Query q, IReadOnlyList<SelectColumn>? selects)
    {
        if (selects is null or { Count: 0 })
        {
            q.SelectRaw("*");
            return;
        }

        foreach (var col in selects)
        {
            if (col.Alias is not null)
                q.SelectRaw($"{col.Expression} AS {QuoteIdentifier(col.Alias)}");
            else
                q.SelectRaw(col.Expression);
        }
    }

    private static void ApplyJoins(Query q, IReadOnlyList<JoinDefinition>? joins)
    {
        if (joins is null) return;
        foreach (var j in joins)
        {
            q.Join(j.TargetTable, j.LeftColumn, j.RightColumn, "=",
                j.Type.ToUpperInvariant() switch
                {
                    "LEFT"  => "left join",
                    "RIGHT" => "right join",
                    "CROSS" => "cross join",
                    _       => "join"
                });
        }
    }

    private void ApplyFilters(Query q, IReadOnlyList<FilterDefinition>? filters)
    {
        if (filters is null) return;
        foreach (var f in filters)
        {
            // If the node specifies a canonical function, resolve it first
            if (f.CanonicalFn is not null && f.FnArgs is not null)
            {
                var fragment = _fnRegistry.GetFunction(f.CanonicalFn, f.FnArgs);
                q.WhereRaw(fragment);
                continue;
            }

            switch (f.Operator.ToUpperInvariant())
            {
                case "IS NULL":
                    q.WhereNull(f.Column);
                    break;

                case "IS NOT NULL":
                    q.WhereNotNull(f.Column);
                    break;

                case "IN" when f.Value is IEnumerable<object> values:
                    q.WhereIn(f.Column, values);
                    break;

                case "NOT IN" when f.Value is IEnumerable<object> values:
                    q.WhereNotIn(f.Column, values);
                    break;

                case "BETWEEN" when f.Value is (object lo, object hi):
                    q.WhereBetween(f.Column, lo, hi);
                    break;

                case "REGEX":
                    // Delegate to registry — the column is the first arg
                    var regexFrag = _fnRegistry.GetFunction(
                        SqlFn.Regex, f.Column, f.Value?.ToString() ?? "''");
                    q.WhereRaw(regexFrag);
                    break;

                default:
                    q.Where(f.Column, f.Operator, f.Value);
                    break;
            }
        }
    }

    private static void ApplyGroupBy(Query q, IReadOnlyList<string>? groups)
    {
        if (groups is null or { Count: 0 }) return;
        q.GroupBy(groups.ToArray());
    }

    private static void ApplyOrders(Query q, IReadOnlyList<OrderDefinition>? orders)
    {
        if (orders is null) return;
        foreach (var o in orders)
        {
            if (o.Descending) q.OrderByDesc(o.Column);
            else              q.OrderBy(o.Column);
        }
    }

    private static void ApplyPagination(Query q, int? limit, int? offset)
    {
        if (limit.HasValue)  q.Limit(limit.Value);
        if (offset.HasValue) q.Offset(offset.Value);
    }

    // ── Compiler factory ──────────────────────────────────────────────────────

    private static Compiler CreateCompiler(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.SqlServer => new SqlServerCompiler(),
        DatabaseProvider.MySql     => new MySqlCompiler(),
        DatabaseProvider.Postgres  => new PostgresCompiler(),
        _ => throw new NotSupportedException($"No SqlKata compiler for provider {provider}.")
    };

    // ── Identifier quoting ────────────────────────────────────────────────────

    private string QuoteIdentifier(string id) => _provider switch
    {
        DatabaseProvider.SqlServer => $"[{id}]",
        DatabaseProvider.MySql     => $"`{id}`",
        DatabaseProvider.Postgres  => $"\"{id}\"",
        _ => id
    };
}
