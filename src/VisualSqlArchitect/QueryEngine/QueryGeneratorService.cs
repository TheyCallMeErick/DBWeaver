using SqlKata;
using SqlKata.Compilers;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.Registry;

namespace VisualSqlArchitect.QueryEngine;

// ─── Final compiled output ────────────────────────────────────────────────────

/// <summary>
/// The final product of the query generator — SQL string + named bindings.
/// Passed to <see cref="IDbOrchestrator.ExecutePreviewAsync"/>.
/// </summary>
public sealed record GeneratedQuery(
    string Sql,
    IReadOnlyDictionary<string, object?> Bindings,
    string DebugTree // human-readable expression tree for the canvas debug panel
);

// ─── Service ──────────────────────────────────────────────────────────────────

/// <summary>
/// Bridges the <see cref="NodeGraph"/> / <see cref="NodeGraphCompiler"/> world
/// with the SqlKata <see cref="Compiler"/> world.
///
/// Pipeline:
/// <code>
///   NodeGraph
///     → NodeGraphCompiler  (resolves expression trees)
///     → CompiledNodeGraph  (ISqlExpression trees per SELECT/WHERE/ORDER/GROUP)
///     → QueryGeneratorService.Generate()
///     → SqlKata Query       (structural: FROM, JOINs, paging)
///     + raw expression strings injected via SelectRaw / WhereRaw
///     → Compiler.Compile()
///     → GeneratedQuery { Sql, Bindings }
/// </code>
/// </summary>
public sealed class QueryGeneratorService(DatabaseProvider provider, ISqlFunctionRegistry registry)
{
    private readonly DatabaseProvider _provider = provider;
    private readonly ISqlFunctionRegistry _registry = registry;
    private readonly Compiler _compiler = CreateCompiler(provider);
    private readonly EmitContext _emitCtx = new EmitContext(provider, registry);

    public static QueryGeneratorService Create(DatabaseProvider provider) =>
        new(provider, new SqlFunctionRegistry(provider));

    // ── Main entry point ──────────────────────────────────────────────────────

    /// <summary>
    /// Compiles a <see cref="NodeGraph"/> plus optional structural overrides into
    /// a provider-correct SQL string.
    /// </summary>
    public GeneratedQuery Generate(
        string fromTable,
        NodeGraph graph,
        IReadOnlyList<JoinDefinition>? joins = null,
        SetOperationDefinition? setOperation = null
    )
    {
        (Query query, CompiledNodeGraph compiled) = BuildQueryFromGraph(fromTable, graph, joins);

        // 8. Compile to SQL
        SqlResult result = _compiler.Compile(query);
        string finalSql = ApplyRecursiveCtePrefix(result.Sql, graph.Ctes);
        finalSql = ApplyQualifyClause(finalSql, compiled.QualifyExprs);
        finalSql = ApplySetOperation(finalSql, setOperation);
        finalSql = ApplyPivotOperation(finalSql, graph.PivotMode, graph.PivotConfig);
        finalSql = ApplyQueryHints(finalSql, graph.QueryHints);

        // 9. Build debug tree string
        string debugTree = BuildDebugTree(compiled);

        return new GeneratedQuery(finalSql, result.NamedBindings, debugTree);
    }

    /// <summary>
    /// Overload that accepts a pre-resolved <see cref="CompiledNodeGraph"/>
    /// (e.g. from the canvas preview panel that already ran the compiler).
    /// </summary>
    public GeneratedQuery Generate(
        string fromTable,
        CompiledNodeGraph compiled,
        IReadOnlyList<JoinDefinition>? joins = null,
        IReadOnlyList<WhereBinding>? whereMeta = null,
        SetOperationDefinition? setOperation = null,
        string? queryHints = null
    )
    {
        Query query = BuildSqlKataQuery(fromTable, compiled, joins, whereMeta ?? []);

        SqlResult result = _compiler.Compile(query);
        string sql = ApplyQualifyClause(result.Sql, compiled.QualifyExprs);
        sql = ApplySetOperation(sql, setOperation);
        sql = ApplyPivotOperation(sql, null, null);
        sql = ApplyQueryHints(sql, queryHints);
        return new GeneratedQuery(sql, result.NamedBindings, BuildDebugTree(compiled));
    }

    private (Query Query, CompiledNodeGraph Compiled) BuildQueryFromGraph(
        string fromTable,
        NodeGraph graph,
        IReadOnlyList<JoinDefinition>? joins = null
    )
    {
        var nodeCompiler = new NodeGraphCompiler(graph, _emitCtx);
        CompiledNodeGraph compiled = nodeCompiler.Compile();
        Query query = BuildSqlKataQuery(fromTable, compiled, joins, graph.WhereConditions);

        ApplyCtes(query, graph.Ctes);
        return (query, compiled);
    }

    private Query BuildSqlKataQuery(
        string fromTable,
        CompiledNodeGraph compiled,
        IReadOnlyList<JoinDefinition>? joins,
        IReadOnlyList<WhereBinding> whereMeta
    )
    {
        Query query = IsSubqueryFromSource(fromTable)
            ? new Query().FromRaw(fromTable)
            : new Query(fromTable);
        ApplyJoins(query, joins);
        ApplySelects(query, compiled.SelectExprs);
        if (compiled.Distinct)
            query.Distinct();
        ApplyWheres(query, compiled.WhereExprs, whereMeta);
        ApplyOrders(query, compiled.OrderExprs);
        ApplyGroupBys(query, compiled.GroupByExprs);
        ApplyHavings(query, compiled.HavingExprs);
        if (compiled.Limit.HasValue)
            query.Limit(compiled.Limit.Value);
        if (compiled.Offset.HasValue)
            query.Offset(compiled.Offset.Value);
        return query;
    }

    private static bool IsSubqueryFromSource(string fromTable)
    {
        if (string.IsNullOrWhiteSpace(fromTable))
            return false;

        return fromTable.TrimStart().StartsWith("(", StringComparison.Ordinal);
    }

    private static string ApplySetOperation(string sql, SetOperationDefinition? setOperation)
    {
        if (setOperation is null)
            return sql;

        if (string.IsNullOrWhiteSpace(setOperation.QuerySql))
            return sql;

        string? normalizedOperator = NormalizeSetOperator(setOperation.Operator);
        if (normalizedOperator is null)
            return sql;

        string left = TrimTrailingSemicolon(sql);
        string right = TrimTrailingSemicolon(setOperation.QuerySql);

        if (string.IsNullOrWhiteSpace(right))
            return sql;

        return $"{left}\n{normalizedOperator}\n{right}";
    }

    private string ApplyQualifyClause(string sql, IReadOnlyList<ISqlExpression> qualifyExprs)
    {
        if (qualifyExprs.Count == 0)
            return sql;

        var predicates = qualifyExprs
            .Select(e => NormalizeWherePredicate(e.Emit(_emitCtx)))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (predicates.Count == 0)
            return sql;

        string predicate = string.Join(" AND ", predicates);
        string inner = TrimTrailingSemicolon(sql);

        // Emulate QUALIFY across providers by filtering over a projected subquery.
        return $"SELECT * FROM ({inner}) AS _qualify\nWHERE {predicate}";
    }

    private string ApplyQueryHints(string sql, string? queryHints)
    {
        if (!QueryHintSyntax.TryNormalize(_provider, queryHints, out string hints, out _)
            || string.IsNullOrWhiteSpace(hints))
            return sql;

        string baseSql = TrimTrailingSemicolon(sql);
        return _provider switch
        {
            DatabaseProvider.SqlServer => ApplySqlServerHints(baseSql, hints),
            DatabaseProvider.MySql => ApplySelectCommentHints(baseSql, hints),
            DatabaseProvider.Postgres => ApplySelectCommentHints(baseSql, hints),
            _ => baseSql,
        };
    }

    private static string ApplySqlServerHints(string sql, string hints)
    {
        if (sql.Contains(" OPTION (", StringComparison.OrdinalIgnoreCase))
            return sql;

        string normalized = hints.StartsWith("OPTION", StringComparison.OrdinalIgnoreCase)
            ? hints
            : $"OPTION ({hints})";

        return $"{sql}\n{normalized}";
    }

    private static string ApplySelectCommentHints(string sql, string hints)
    {
        int selectIndex = sql.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
        if (selectIndex < 0)
            return sql;

        int insertAt = selectIndex + 6; // after SELECT
        return sql.Insert(insertAt, $" /*+ {hints} */");
    }

    private string ApplyPivotOperation(string sql, string? pivotMode, string? pivotConfig)
    {
        if (_provider != DatabaseProvider.SqlServer)
            return sql;

        string mode = (pivotMode ?? "NONE").Trim().ToUpperInvariant();
        if (mode is not ("PIVOT" or "UNPIVOT"))
            return sql;

        string config = (pivotConfig ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(config) || config.Contains(';', StringComparison.Ordinal))
            return sql;

        string baseSql = TrimTrailingSemicolon(sql);
        string sourceAlias = mode == "PIVOT" ? "_pivot_src" : "_unpivot_src";
        string targetAlias = mode == "PIVOT" ? "_pivot" : "_unpivot";
        return $"SELECT * FROM ({baseSql}) AS {sourceAlias}\n{mode} ({config}) AS {targetAlias}";
    }

    private static string? NormalizeSetOperator(string? @operator)
    {
        if (string.IsNullOrWhiteSpace(@operator))
            return null;

        return @operator.Trim().ToUpperInvariant() switch
        {
            "UNION" => "UNION",
            "UNION ALL" => "UNION ALL",
            "INTERSECT" => "INTERSECT",
            "EXCEPT" => "EXCEPT",
            _ => null,
        };
    }

    private static string TrimTrailingSemicolon(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return string.Empty;

        return sql.Trim().TrimEnd(';').TrimEnd();
    }

    private void ApplyCtes(Query query, IReadOnlyList<CteBinding> ctes)
    {
        foreach (CteBinding cte in OrderCtesByDependencies(ctes))
        {
            (Query cteQuery, _) = BuildQueryFromGraph(cte.FromTable, cte.Graph);
            query.With(cte.Name, cteQuery);
        }
    }

    private static IReadOnlyList<CteBinding> OrderCtesByDependencies(IReadOnlyList<CteBinding> ctes)
    {
        if (ctes.Count == 0)
            return ctes;

        var byName = new Dictionary<string, CteBinding>(StringComparer.OrdinalIgnoreCase);
        var originalOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < ctes.Count; i++)
        {
            CteBinding cte = ctes[i];
            if (!byName.TryAdd(cte.Name, cte))
                throw new InvalidOperationException($"Duplicate CTE name '{cte.Name}'.");
            originalOrder[cte.Name] = i;
        }

        var adjacency = byName.Values.ToDictionary(
            c => c.Name,
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase
        );
        var inDegree = byName.Values.ToDictionary(
            c => c.Name,
            _ => 0,
            StringComparer.OrdinalIgnoreCase
        );

        foreach (CteBinding cte in byName.Values)
        {
            foreach (string dep in GetCteDependencies(cte, byName.Keys))
            {
                if (dep.Equals(cte.Name, StringComparison.OrdinalIgnoreCase))
                {
                    if (!cte.Recursive)
                    {
                        throw new InvalidOperationException(
                            $"CTE '{cte.Name}' references itself but is not marked recursive."
                        );
                    }

                    continue;
                }

                // dependency -> current edge
                if (adjacency[dep].Add(cte.Name))
                    inDegree[cte.Name]++;
            }
        }

        var ready = inDegree
            .Where(kv => kv.Value == 0)
            .Select(kv => kv.Key)
            .OrderBy(name => originalOrder[name])
            .ToList();

        var sortedNames = new List<string>(ctes.Count);
        while (ready.Count > 0)
        {
            string name = ready[0];
            ready.RemoveAt(0);
            sortedNames.Add(name);

            foreach (string next in adjacency[name])
            {
                inDegree[next]--;
                if (inDegree[next] == 0)
                {
                    ready.Add(next);
                    ready.Sort((a, b) => originalOrder[a].CompareTo(originalOrder[b]));
                }
            }
        }

        if (sortedNames.Count != ctes.Count)
        {
            IEnumerable<string> cycle = inDegree.Where(kv => kv.Value > 0).Select(kv => kv.Key);
            throw new InvalidOperationException(
                "Cycle detected between CTE definitions: " + string.Join(", ", cycle)
            );
        }

        return sortedNames.Select(name => byName[name]).ToList();
    }

    private static IReadOnlyList<string> GetCteDependencies(
        CteBinding cte,
        IEnumerable<string> knownCteNames
    )
    {
        var known = new HashSet<string>(knownCteNames, StringComparer.OrdinalIgnoreCase);
        var deps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(cte.FromTable))
        {
            string fromRef = cte.FromTable.Trim();
            string normalizedFromRef = NormalizeFromReferenceName(fromRef);
            if (known.Contains(normalizedFromRef))
                deps.Add(normalizedFromRef);
        }

        foreach (NodeInstance node in cte.Graph.Nodes)
        {
            if (node.Type != NodeType.CteSource)
                continue;

            if (
                node.Parameters.TryGetValue("cte_name", out string? dep)
                && !string.IsNullOrWhiteSpace(dep)
                && known.Contains(dep.Trim())
            )
            {
                deps.Add(dep.Trim());
            }
        }

        return deps.ToList();
    }

    private static string NormalizeFromReferenceName(string fromReference)
    {
        if (string.IsNullOrWhiteSpace(fromReference))
            return string.Empty;

        string firstToken = fromReference
            .Trim()
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0];

        if (firstToken.Length >= 2)
        {
            if (
                (firstToken[0] == '"' && firstToken[^1] == '"')
                || (firstToken[0] == '`' && firstToken[^1] == '`')
            )
            {
                return firstToken[1..^1];
            }

            if (firstToken[0] == '[' && firstToken[^1] == ']')
                return firstToken[1..^1];
        }

        return firstToken;
    }

    private string ApplyRecursiveCtePrefix(string sql, IReadOnlyList<CteBinding> ctes)
    {
        if (!ctes.Any(c => c.Recursive))
            return sql;

        // SQL Server recursive CTEs do not use the RECURSIVE keyword.
        if (_provider == DatabaseProvider.SqlServer)
            return sql;

        if (!sql.StartsWith("WITH ", StringComparison.OrdinalIgnoreCase))
            return sql;

        if (sql.StartsWith("WITH RECURSIVE ", StringComparison.OrdinalIgnoreCase))
            return sql;

        return "WITH RECURSIVE " + sql[5..];
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CLAUSE INJECTORS
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplySelects(Query q, IReadOnlyList<(ISqlExpression Expr, string? Alias)> selects)
    {
        if (selects.Count == 0)
        {
            q.SelectRaw("*");
            return;
        }

        foreach ((ISqlExpression expr, string? alias) in selects)
        {
            string sql = expr.Emit(_emitCtx);
            q.SelectRaw(alias is null ? sql : $"{sql} AS {_emitCtx.QuoteIdentifier(alias)}");
        }
    }

    private static void ApplyJoins(Query q, IReadOnlyList<JoinDefinition>? joins)
    {
        if (joins is null)
            return;
        foreach (JoinDefinition j in joins)
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
                q.Join(j.TargetTable, x => x.WhereRaw(j.OnRaw), joinType);
                continue;
            }

            q.Join(
                j.TargetTable,
                j.LeftColumn,
                j.RightColumn,
                string.IsNullOrWhiteSpace(j.Operator) ? "=" : j.Operator,
                joinType
            );
        }
    }

    private void ApplyWheres(
        Query q,
        IReadOnlyList<ISqlExpression> whereExprs,
        IReadOnlyList<WhereBinding> bindings
    )
    {
        if (whereExprs.Count == 0)
            return;

        var predicates = new List<(string Sql, string Op)>();
        for (int i = 0; i < whereExprs.Count; i++)
        {
            string op = i < bindings.Count ? bindings[i].LogicOp : "AND";
            string normalized = NormalizeWherePredicate(whereExprs[i].Emit(_emitCtx));
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            predicates.Add((normalized, op));
        }

        if (predicates.Count == 0)
            return;

        // When there's only one WHERE expression, inject it directly
        if (predicates.Count == 1)
        {
            q.WhereRaw(predicates[0].Sql);
            return;
        }

        // Multiple expressions: respect the LogicOp of each WhereBinding
        // Group consecutive OR bindings into sub-expressions, AND them together
        var andGroups = new List<List<string>>();
        var currentOr = new List<string>();

        foreach ((string sql, string op) in predicates)
        {
            if (op.Equals("OR", StringComparison.OrdinalIgnoreCase))
            {
                currentOr.Add(sql);
            }
            else
            {
                if (currentOr.Count > 0)
                {
                    andGroups.Add([.. currentOr]);
                    currentOr.Clear();
                }
                andGroups.Add([sql]);
            }
        }
        if (currentOr.Count > 0)
            andGroups.Add(currentOr);

        foreach (List<string> group in andGroups)
        {
            if (group.Count == 1)
            {
                q.WhereRaw(group[0]);
            }
            else
            {
                // Build (expr1 OR expr2 OR ...)
                string orClause = "(" + string.Join(" OR ", group) + ")";
                q.WhereRaw(orClause);
            }
        }
    }

    private string NormalizeWherePredicate(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return string.Empty;

        string trimmed = sql.Trim();
        if (trimmed.Equals("NULL", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        if (trimmed.Equals("TRUE", StringComparison.OrdinalIgnoreCase))
        {
            return _provider == DatabaseProvider.SqlServer ? "1 = 1" : "TRUE";
        }

        if (trimmed.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
        {
            return _provider == DatabaseProvider.SqlServer ? "1 = 0" : "FALSE";
        }

        return trimmed;
    }

    private void ApplyOrders(Query q, IReadOnlyList<(ISqlExpression Expr, bool Desc)> orders)
    {
        foreach ((ISqlExpression expr, bool desc) in orders)
        {
            string sql = expr.Emit(_emitCtx);
            if (desc)
                q.OrderByRaw($"{sql} DESC");
            else
                q.OrderByRaw(sql);
        }
    }

    private void ApplyGroupBys(Query q, IReadOnlyList<ISqlExpression> groups)
    {
        foreach (ISqlExpression g in groups)
            q.GroupByRaw(g.Emit(_emitCtx));
    }

    private void ApplyHavings(Query q, IReadOnlyList<ISqlExpression> havings)
    {
        foreach (ISqlExpression h in havings)
            q.HavingRaw(h.Emit(_emitCtx));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DEBUG TREE
    // ─────────────────────────────────────────────────────────────────────────

    private string BuildDebugTree(CompiledNodeGraph compiled)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("── SELECT ─────────────────────────────────────");
        if (compiled.Distinct)
            sb.AppendLine("  DISTINCT");
        if (compiled.SelectExprs.Count == 0)
            sb.AppendLine("  * (all columns)");
        foreach ((ISqlExpression expr, string? alias) in compiled.SelectExprs)
        {
            string sql = expr.Emit(_emitCtx);
            sb.AppendLine(alias is null ? $"  {sql}" : $"  {sql}  →  {alias}");
        }

        if (compiled.WhereExprs.Count > 0)
        {
            sb.AppendLine("── WHERE ──────────────────────────────────────");
            foreach (ISqlExpression expr in compiled.WhereExprs)
                sb.AppendLine($"  {expr.Emit(_emitCtx)}");
        }

        if (compiled.GroupByExprs.Count > 0)
        {
            sb.AppendLine("── GROUP BY ───────────────────────────────────");
            foreach (ISqlExpression expr in compiled.GroupByExprs)
                sb.AppendLine($"  {expr.Emit(_emitCtx)}");
        }

        if (compiled.HavingExprs.Count > 0)
        {
            sb.AppendLine("── HAVING ─────────────────────────────────────");
            foreach (ISqlExpression expr in compiled.HavingExprs)
                sb.AppendLine($"  {expr.Emit(_emitCtx)}");
        }

        if (compiled.QualifyExprs.Count > 0)
        {
            sb.AppendLine("── QUALIFY ────────────────────────────────────");
            foreach (ISqlExpression expr in compiled.QualifyExprs)
                sb.AppendLine($"  {expr.Emit(_emitCtx)}");
        }

        if (compiled.OrderExprs.Count > 0)
        {
            sb.AppendLine("── ORDER BY ───────────────────────────────────");
            foreach ((ISqlExpression expr, bool desc) in compiled.OrderExprs)
                sb.AppendLine($"  {expr.Emit(_emitCtx)} {(desc ? "DESC" : "ASC")}");
        }

        return sb.ToString();
    }

    // ── Compiler factory ──────────────────────────────────────────────────────

    private static Compiler CreateCompiler(DatabaseProvider p) =>
        p switch
        {
            DatabaseProvider.SqlServer => new SqlServerCompiler(),
            DatabaseProvider.MySql => new MySqlCompiler(),
            DatabaseProvider.Postgres => new PostgresCompiler(),
            DatabaseProvider.SQLite => new SqliteCompiler(),
            _ => throw new NotSupportedException($"No SqlKata compiler for {p}."),
        };
}
