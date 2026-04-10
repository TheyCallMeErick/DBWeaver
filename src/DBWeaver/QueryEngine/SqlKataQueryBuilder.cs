using SqlKata;
using SqlKata.Compilers;
using DBWeaver.Core;
using DBWeaver.Registry;

namespace DBWeaver.QueryEngine;

/// <summary>
/// SqlKata-based implementation of IQueryBuilder.
/// Adapts SqlKata Query API to the IQueryBuilder interface.
/// </summary>
public sealed class SqlKataQueryBuilder : IQueryBuilder
{
    private readonly Query _query;
    private readonly Compiler _compiler;
    private readonly ISqlFunctionRegistry _fnRegistry;
    private readonly DatabaseProvider _provider;

    public SqlKataQueryBuilder(string fromTable, DatabaseProvider provider, ISqlFunctionRegistry fnRegistry)
    {
        _query = new Query(fromTable);
        _compiler = CreateCompiler(provider);
        _fnRegistry = fnRegistry;
        _provider = provider;
    }

    public IQueryBuilder Select(IEnumerable<SelectColumn> columns)
    {
        var colList = columns?.ToList();
        if (colList is null or { Count: 0 })
        {
            _query.SelectRaw("*");
            return this;
        }

        foreach (var col in colList)
        {
            if (col.Alias is not null)
                _query.SelectRaw($"{col.Expression} AS {QuoteIdentifier(col.Alias)}");
            else
                _query.SelectRaw(col.Expression);
        }

        return this;
    }

    public IQueryBuilder Join(IEnumerable<JoinDefinition> joins)
    {
        if (joins is null)
            return this;

        foreach (var j in joins)
        {
            string joinType = j.Type.ToUpperInvariant() switch
            {
                "LEFT" => "left join",
                "RIGHT" => "right join",
                "FULL" => "full join",
                "CROSS" => "cross join",
                _ => "join",
            };

            if (!string.IsNullOrWhiteSpace(j.OnRaw))
            {
                _query.Join(j.TargetTable, x => x.WhereRaw(j.OnRaw), joinType);
                continue;
            }

            _query.Join(
                j.TargetTable,
                j.LeftColumn,
                j.RightColumn,
                string.IsNullOrWhiteSpace(j.Operator) ? "=" : j.Operator,
                joinType
            );
        }

        return this;
    }

    public IQueryBuilder Filter(IEnumerable<FilterDefinition> filters)
    {
        if (filters is null)
            return this;

        foreach (var f in filters)
        {
            // If the node specifies a canonical function, resolve it first
            if (f.CanonicalFn is not null && f.FnArgs is not null)
            {
                string fragment = _fnRegistry.GetFunction(f.CanonicalFn, f.FnArgs);
                _query.WhereRaw(fragment);
                continue;
            }

            switch (f.Operator.ToUpperInvariant())
            {
                case "IS NULL":
                    _query.WhereNull(f.Column);
                    break;

                case "IS NOT NULL":
                    _query.WhereNotNull(f.Column);
                    break;

                case "IN" when f.Value is IEnumerable<object> values:
                    _query.WhereIn(f.Column, values);
                    break;

                case "NOT IN" when f.Value is IEnumerable<object> values:
                    _query.WhereNotIn(f.Column, values);
                    break;

                case "BETWEEN" when f.Value is (object lo, object hi):
                    _query.WhereBetween(f.Column, lo, hi);
                    break;

                case "REGEX":
                    // Delegate to registry — the column is the first arg
                    string regexFrag = _fnRegistry.GetFunction(
                        SqlFn.Regex,
                        f.Column,
                        f.Value?.ToString() ?? "''"
                    );
                    _query.WhereRaw(regexFrag);
                    break;

                default:
                    _query.Where(f.Column, f.Operator, f.Value);
                    break;
            }
        }

        return this;
    }

    public IQueryBuilder GroupBy(IEnumerable<string> columns)
    {
        var colList = columns?.ToList();
        if (colList is null or { Count: 0 })
            return this;

        _query.GroupBy([..colList]);
        return this;
    }

    public IQueryBuilder OrderBy(IEnumerable<OrderDefinition> orders)
    {
        if (orders is null)
            return this;

        foreach (var o in orders)
        {
            if (o.Descending)
                _query.OrderByDesc(o.Column);
            else
                _query.OrderBy(o.Column);
        }

        return this;
    }

    public IQueryBuilder Limit(int? limit, int? offset)
    {
        if (limit.HasValue)
            _query.Limit(limit.Value);
        if (offset.HasValue)
            _query.Offset(offset.Value);

        return this;
    }

    public CompiledQuery Compile()
    {
        SqlResult result = _compiler.Compile(_query);
        var bindings = result.NamedBindings;
        return new CompiledQuery(result.Sql, bindings);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Compiler CreateCompiler(DatabaseProvider provider) =>
        provider switch
        {
            DatabaseProvider.SqlServer => new SqlServerCompiler(),
            DatabaseProvider.MySql => new MySqlCompiler(),
            DatabaseProvider.Postgres => new PostgresCompiler(),
            DatabaseProvider.SQLite => new SqliteCompiler(),
            _ => throw new NotSupportedException($"No SqlKata compiler for provider {provider}."),
        };

    private string QuoteIdentifier(string id) =>
        _provider switch
        {
            DatabaseProvider.SqlServer => $"[{id}]",
            DatabaseProvider.MySql => $"`{id}`",
            DatabaseProvider.Postgres => $"\"{id}\"",
            _ => id,
        };
}

