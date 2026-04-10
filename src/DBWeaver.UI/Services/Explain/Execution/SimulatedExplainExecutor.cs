using System.Text.RegularExpressions;
using DBWeaver.UI.ViewModels.Canvas;
using DBWeaver.Core;

namespace DBWeaver.UI.Services.Explain;

public sealed class SimulatedExplainExecutor : IExplainExecutor
{
    public Task<ExplainResult> RunAsync(
        string sql,
        DatabaseProvider provider,
        ConnectionConfig? connectionConfig,
        ExplainOptions options,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        IReadOnlyList<ExplainNode> nodes = provider switch
        {
            DatabaseProvider.Postgres => SimulatePostgres(sql),
            DatabaseProvider.MySql => SimulateMySql(sql),
            DatabaseProvider.SqlServer => SimulateSqlServer(sql),
            DatabaseProvider.SQLite => SimulatePostgres(sql),
            _ => SimulatePostgres(sql),
        };

        return Task.FromResult(
            new ExplainResult(
                Nodes: nodes,
                PlanningTimeMs: null,
                ExecutionTimeMs: null,
                RawOutput: "SIMULATED",
                IsSimulated: true
            )
        );
    }

    private sealed record SqlContext(
        List<string> Tables,
        bool HasWhere,
        bool HasOrderBy,
        bool HasLimit,
        bool HasJoin,
        int JoinCount
    );

    private static SqlContext ParseSql(string sql)
    {
        string up = sql.ToUpperInvariant();
        var tableMatches = Regex.Matches(
            sql,
            @"(?:FROM|JOIN)\s+([a-zA-Z_][a-zA-Z0-9_]*)",
            RegexOptions.IgnoreCase
        );
        var tables = tableMatches
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (tables.Count == 0)
            tables.Add("table1");

        int joinCount = Regex.Matches(up, @"\bJOIN\b").Count;
        return new SqlContext(
            Tables: tables,
            HasWhere: up.Contains("WHERE"),
            HasOrderBy: up.Contains("ORDER BY"),
            HasLimit: up.Contains("LIMIT") || up.Contains("TOP"),
            HasJoin: joinCount > 0,
            JoinCount: joinCount
        );
    }

    private static IReadOnlyList<ExplainNode> SimulatePostgres(string sql)
    {
        var ctx = ParseSql(sql);
        var nodes = new List<ExplainNode>();
        var rng = new Random(sql.Length ^ 0x1A2B3C);

        double baseCost = rng.Next(200, 600);
        double scanCost1 = rng.Next(100, 400);
        long scanRows1 = rng.Next(1000, 50000);
        double totalCost = baseCost + scanCost1;

        if (ctx.HasLimit)
            nodes.Add(new ExplainNode { NodeType = "Limit", Detail = $"cost=0.00..{totalCost:F2} rows=100 width=64" });

        int topIndent = ctx.HasLimit ? 1 : 0;
        if (ctx.HasJoin)
        {
            nodes.Add(
                new ExplainNode
                {
                    NodeType = "Hash Join",
                    Detail = $"cost={baseCost:F2}..{totalCost:F2} rows={scanRows1 / 10} width=64",
                    EstimatedCost = totalCost,
                    EstimatedRows = scanRows1 / 10,
                    IndentLevel = topIndent,
                    AlertLabel = "HASH",
                }
            );

            bool useIndex1 = rng.Next(2) == 0;
            nodes.Add(
                new ExplainNode
                {
                    NodeType = useIndex1 ? $"Index Scan on {ctx.Tables[0]}" : $"Seq Scan on {ctx.Tables[0]}",
                    Detail = $"cost=0.00..{scanCost1:F2} rows={scanRows1} width=32",
                    EstimatedCost = scanCost1,
                    EstimatedRows = scanRows1,
                    IndentLevel = topIndent + 1,
                    IsExpensive = !useIndex1,
                    AlertLabel = useIndex1 ? string.Empty : "SEQ SCAN",
                }
            );
        }
        else
        {
            bool useIndex = rng.Next(3) != 0;
            nodes.Add(
                new ExplainNode
                {
                    NodeType = useIndex ? $"Index Scan on {ctx.Tables[0]}" : $"Seq Scan on {ctx.Tables[0]}",
                    Detail = $"cost=0.00..{scanCost1:F2} rows={scanRows1} width=64",
                    EstimatedCost = scanCost1,
                    EstimatedRows = scanRows1,
                    IndentLevel = topIndent,
                    IsExpensive = !useIndex,
                    AlertLabel = useIndex ? string.Empty : "SEQ SCAN",
                }
            );
        }

        if (ctx.HasOrderBy)
        {
            nodes.Insert(
                ctx.HasLimit ? 1 : 0,
                new ExplainNode
                {
                    NodeType = "Sort",
                    Detail = $"cost={baseCost / 2:F2}..{baseCost:F2} rows={scanRows1 / 5} width=64",
                    EstimatedCost = baseCost,
                    EstimatedRows = scanRows1 / 5,
                    IndentLevel = topIndent,
                    IsExpensive = true,
                    AlertLabel = "SORT",
                }
            );
        }

        return nodes;
    }

    private static IReadOnlyList<ExplainNode> SimulateMySql(string sql)
    {
        var ctx = ParseSql(sql);
        var nodes = new List<ExplainNode>();
        var rng = new Random(sql.Length ^ 0x2C3D4E);

        foreach (string table in ctx.Tables)
        {
            bool useIndex = rng.Next(3) != 0;
            long rows = rng.Next(100, 50000);
            bool hasExtra = ctx.HasWhere && table == ctx.Tables[0];

            string type = useIndex ? "ref" : "ALL";
            string key = useIndex ? "idx_" + table : "NULL";
            string extra = hasExtra ? "Using where" : (ctx.HasOrderBy ? "Using filesort" : "");
            bool isFilesort = extra.Contains("filesort", StringComparison.OrdinalIgnoreCase);

            nodes.Add(
                new ExplainNode
                {
                    NodeType = table,
                    Detail = $"select_type=SIMPLE type={type} key={key} Extra={extra}",
                    EstimatedRows = rows,
                    IsExpensive = !useIndex || isFilesort,
                    AlertLabel = !useIndex ? "SEQ SCAN" : (isFilesort ? "SORT" : string.Empty),
                }
            );
        }

        return nodes;
    }

    private static IReadOnlyList<ExplainNode> SimulateSqlServer(string sql)
    {
        var ctx = ParseSql(sql);
        var nodes = new List<ExplainNode>();
        var rng = new Random(sql.Length ^ 0x3E4F5A);

        if (ctx.HasLimit)
        {
            nodes.Add(
                new ExplainNode
                {
                    NodeType = "Top",
                    Detail = "TOP EXPRESSION: (100)",
                    EstimatedCost = 0.01,
                    EstimatedRows = 100,
                }
            );
        }

        int topIndent = ctx.HasLimit ? 1 : 0;

        if (ctx.HasJoin)
        {
            nodes.Add(
                new ExplainNode
                {
                    NodeType = "Nested Loops",
                    Detail = "Inner Join",
                    EstimatedCost = rng.Next(100, 500),
                    EstimatedRows = rng.Next(500, 5000),
                    IndentLevel = topIndent,
                    AlertLabel = "LOOP",
                }
            );
        }
        else
        {
            bool useIndex = rng.Next(3) != 0;
            long rows = rng.Next(1000, 50000);
            nodes.Add(
                new ExplainNode
                {
                    NodeType = useIndex ? $"Index Seek ({ctx.Tables[0]})" : $"Table Scan ({ctx.Tables[0]})",
                    Detail = ctx.HasWhere ? "Predicate: condition" : null,
                    EstimatedCost = rng.Next(50, 400),
                    EstimatedRows = rows,
                    IndentLevel = topIndent,
                    IsExpensive = !useIndex,
                    AlertLabel = useIndex ? string.Empty : "SEQ SCAN",
                }
            );
        }

        if (ctx.HasOrderBy)
        {
            nodes.Insert(
                ctx.HasLimit ? 1 : 0,
                new ExplainNode
                {
                    NodeType = "Sort",
                    Detail = "ORDER BY",
                    EstimatedCost = rng.Next(100, 800),
                    EstimatedRows = rng.Next(500, 5000),
                    IndentLevel = topIndent,
                    IsExpensive = true,
                    AlertLabel = "SORT",
                }
            );
        }

        return nodes;
    }
}



