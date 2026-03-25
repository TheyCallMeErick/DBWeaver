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
    string DebugTree    // human-readable expression tree for the canvas debug panel
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
public sealed class QueryGeneratorService
{
    private readonly DatabaseProvider _provider;
    private readonly ISqlFunctionRegistry _registry;
    private readonly Compiler _compiler;
    private readonly EmitContext _emitCtx;

    // ── JOIN definitions stay in the VisualQuerySpec (not in NodeGraph) ───────
    // The canvas passes JOINs separately because they are structural
    // (two DataSource nodes wired together via the auto-join suggestion system).

    public QueryGeneratorService(DatabaseProvider provider, ISqlFunctionRegistry registry)
    {
        _provider = provider;
        _registry = registry;
        _compiler = CreateCompiler(provider);
        _emitCtx  = new EmitContext(provider, registry);
    }

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
        IReadOnlyList<JoinDefinition>? joins = null)
    {
        // 1. Resolve all nodes into expression trees
        var nodeCompiler = new NodeGraphCompiler(graph, _emitCtx);
        var compiled     = nodeCompiler.Compile();

        // 2. Build the SqlKata query skeleton (FROM + JOINs + paging)
        var query = new Query(fromTable);
        ApplyJoins(query, joins);

        // 3. Inject SELECT expressions
        ApplySelects(query, compiled.SelectExprs);

        // 4. Inject WHERE expressions
        ApplyWheres(query, compiled.WhereExprs, graph.WhereConditions);

        // 5. Inject ORDER BY
        ApplyOrders(query, compiled.OrderExprs);

        // 6. Inject GROUP BY
        ApplyGroupBys(query, compiled.GroupByExprs);

        // 7. Pagination
        if (compiled.Limit.HasValue)  query.Limit(compiled.Limit.Value);
        if (compiled.Offset.HasValue) query.Offset(compiled.Offset.Value);

        // 8. Compile to SQL
        var result = _compiler.Compile(query);

        // 9. Build debug tree string
        var debugTree = BuildDebugTree(compiled);

        return new GeneratedQuery(result.Sql, result.NamedBindings, debugTree);
    }

    /// <summary>
    /// Overload that accepts a pre-resolved <see cref="CompiledNodeGraph"/>
    /// (e.g. from the canvas preview panel that already ran the compiler).
    /// </summary>
    public GeneratedQuery Generate(
        string fromTable,
        CompiledNodeGraph compiled,
        IReadOnlyList<JoinDefinition>? joins = null,
        IReadOnlyList<WhereBinding>? whereMeta = null)
    {
        var query = new Query(fromTable);
        ApplyJoins(query, joins);
        ApplySelects(query, compiled.SelectExprs);
        ApplyWheres(query, compiled.WhereExprs, whereMeta ?? Array.Empty<WhereBinding>());
        ApplyOrders(query, compiled.OrderExprs);
        ApplyGroupBys(query, compiled.GroupByExprs);
        if (compiled.Limit.HasValue)  query.Limit(compiled.Limit.Value);
        if (compiled.Offset.HasValue) query.Offset(compiled.Offset.Value);

        var result = _compiler.Compile(query);
        return new GeneratedQuery(result.Sql, result.NamedBindings, BuildDebugTree(compiled));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CLAUSE INJECTORS
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplySelects(Query q,
        IReadOnlyList<(ISqlExpression Expr, string? Alias)> selects)
    {
        if (selects.Count == 0) { q.SelectRaw("*"); return; }

        foreach (var (expr, alias) in selects)
        {
            var sql = expr.Emit(_emitCtx);
            q.SelectRaw(alias is null ? sql : $"{sql} AS {_emitCtx.QuoteIdentifier(alias)}");
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

    private void ApplyWheres(Query q,
        IReadOnlyList<ISqlExpression> whereExprs,
        IReadOnlyList<WhereBinding> bindings)
    {
        if (whereExprs.Count == 0) return;

        // When there's only one WHERE expression, inject it directly
        if (whereExprs.Count == 1)
        {
            q.WhereRaw(whereExprs[0].Emit(_emitCtx));
            return;
        }

        // Multiple expressions: respect the LogicOp of each WhereBinding
        // Group consecutive OR bindings into sub-expressions, AND them together
        var andGroups  = new List<List<ISqlExpression>>();
        var currentOr  = new List<ISqlExpression>();

        for (int i = 0; i < whereExprs.Count; i++)
        {
            var op = i < bindings.Count ? bindings[i].LogicOp : "AND";

            if (op.Equals("OR", StringComparison.OrdinalIgnoreCase))
            {
                currentOr.Add(whereExprs[i]);
            }
            else
            {
                if (currentOr.Count > 0)
                {
                    andGroups.Add(currentOr.ToList());
                    currentOr.Clear();
                }
                andGroups.Add(new List<ISqlExpression> { whereExprs[i] });
            }
        }
        if (currentOr.Count > 0) andGroups.Add(currentOr);

        foreach (var group in andGroups)
        {
            if (group.Count == 1)
            {
                q.WhereRaw(group[0].Emit(_emitCtx));
            }
            else
            {
                // Build (expr1 OR expr2 OR ...)
                var orClause = "(" + string.Join(" OR ",
                    group.Select(e => e.Emit(_emitCtx))) + ")";
                q.WhereRaw(orClause);
            }
        }
    }

    private void ApplyOrders(Query q,
        IReadOnlyList<(ISqlExpression Expr, bool Desc)> orders)
    {
        foreach (var (expr, desc) in orders)
        {
            var sql = expr.Emit(_emitCtx);
            if (desc) q.OrderByRaw($"{sql} DESC");
            else      q.OrderByRaw(sql);
        }
    }

    private void ApplyGroupBys(Query q, IReadOnlyList<ISqlExpression> groups)
    {
        foreach (var g in groups)
            q.GroupByRaw(g.Emit(_emitCtx));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DEBUG TREE
    // ─────────────────────────────────────────────────────────────────────────

    private string BuildDebugTree(CompiledNodeGraph compiled)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("── SELECT ─────────────────────────────────────");
        if (compiled.SelectExprs.Count == 0)
            sb.AppendLine("  * (all columns)");
        foreach (var (expr, alias) in compiled.SelectExprs)
        {
            var sql = expr.Emit(_emitCtx);
            sb.AppendLine(alias is null ? $"  {sql}" : $"  {sql}  →  {alias}");
        }

        if (compiled.WhereExprs.Count > 0)
        {
            sb.AppendLine("── WHERE ──────────────────────────────────────");
            foreach (var expr in compiled.WhereExprs)
                sb.AppendLine($"  {expr.Emit(_emitCtx)}");
        }

        if (compiled.GroupByExprs.Count > 0)
        {
            sb.AppendLine("── GROUP BY ───────────────────────────────────");
            foreach (var expr in compiled.GroupByExprs)
                sb.AppendLine($"  {expr.Emit(_emitCtx)}");
        }

        if (compiled.OrderExprs.Count > 0)
        {
            sb.AppendLine("── ORDER BY ───────────────────────────────────");
            foreach (var (expr, desc) in compiled.OrderExprs)
                sb.AppendLine($"  {expr.Emit(_emitCtx)} {(desc ? "DESC" : "ASC")}");
        }

        return sb.ToString();
    }

    // ── Compiler factory ──────────────────────────────────────────────────────

    private static Compiler CreateCompiler(DatabaseProvider p) => p switch
    {
        DatabaseProvider.SqlServer => new SqlServerCompiler(),
        DatabaseProvider.MySql     => new MySqlCompiler(),
        DatabaseProvider.Postgres  => new PostgresCompiler(),
        _ => throw new NotSupportedException($"No SqlKata compiler for {p}.")
    };
}
