namespace VisualSqlArchitect.UI.ViewModels;

// ── Guardrail model ───────────────────────────────────────────────────────────

public enum GuardSeverity
{
    Warning,
    Block,
}

public sealed record GuardIssue(
    GuardSeverity Severity,
    string Code,
    string Message,
    string Suggestion
);

// ── Pre-execution safety rules ────────────────────────────────────────────────

/// <summary>
/// Evaluates a set of configurable safety rules against raw SQL before preview
/// execution. Rules are intentionally lenient (warn-first) to avoid blocking
/// legitimate queries; only genuinely dangerous patterns are classified as Block.
/// </summary>
public static partial class QueryGuardrails
{
    /// <summary>Runs all rules against <paramref name="sql"/> and returns issues.</summary>
    public static IReadOnlyList<GuardIssue> Check(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return [];

        // Normalise for matching: collapse whitespace, upper-case
        string normalised = MyRegex().Replace(sql.Trim(), " ").ToUpperInvariant();

        // Only run SELECT-specific rules on SELECT statements
        bool isSelect =
            normalised.StartsWith("SELECT", StringComparison.Ordinal)
            || normalised.StartsWith("WITH ", StringComparison.Ordinal); // CTE

        var issues = new List<GuardIssue>();

        if (isSelect)
        {
            CheckRequireLimit(normalised, issues);
            CheckSelectStar(normalised, issues);
            CheckNoWhereFilter(normalised, issues);
        }

        return issues;
    }

    // ── Rule: LIMIT / TOP / FETCH NEXT required ───────────────────────────────

    private static void CheckRequireLimit(string sql, List<GuardIssue> issues)
    {
        bool hasLimit =
            sql.Contains(" LIMIT ")
            || sql.Contains("\nLIMIT ")
            || sql.Contains(" LIMIT\n")
            || sql.Contains(" TOP ")
            || sql.Contains("FETCH NEXT")
            || sql.Contains("FETCH FIRST")
            || sql.Contains("ROWNUM ");

        if (!hasLimit)
            issues.Add(
                new GuardIssue(
                    GuardSeverity.Warning,
                    "NO_LIMIT",
                    "Query has no row limit — may return very large result sets",
                    "Add LIMIT <n> (PostgreSQL/MySQL) or TOP <n> (SQL Server) to cap result size"
                )
            );
    }

    // ── Rule: SELECT * ────────────────────────────────────────────────────────

    private static void CheckSelectStar(string sql, List<GuardIssue> issues)
    {
        // Match "SELECT *" or "SELECT DISTINCT *" but not "COUNT(*)"
        bool hasStar =
            sql.Contains("SELECT *")
            || sql.Contains("SELECT DISTINCT *")
            || sql.Contains("SELECT\n*")
            || sql.Contains("SELECT\r\n*");

        if (hasStar)
            issues.Add(
                new GuardIssue(
                    GuardSeverity.Warning,
                    "SELECT_STAR",
                    "SELECT * fetches all columns — may be slow on wide tables and obscures schema intent",
                    "Specify only the columns you need (e.g. SELECT id, name, created_at)"
                )
            );
    }

    // ── Rule: no WHERE / HAVING filter ───────────────────────────────────────

    private static void CheckNoWhereFilter(string sql, List<GuardIssue> issues)
    {
        bool hasWhere = sql.Contains(" WHERE ") || sql.Contains("\nWHERE ");
        bool hasHaving = sql.Contains(" HAVING ") || sql.Contains("\nHAVING ");
        bool hasGroupBy = sql.Contains(" GROUP BY ") || sql.Contains("\nGROUP BY ");
        bool hasLimit =
            sql.Contains(" LIMIT ")
            || sql.Contains("\nLIMIT ")
            || sql.Contains(" LIMIT\n")
            || sql.Contains(" TOP ")
            || sql.Contains("FETCH NEXT")
            || sql.Contains("FETCH FIRST")
            || sql.Contains("ROWNUM ");

        // Suppress when there is already a row-count cap (LIMIT / TOP) — the user has
        // deliberately bounded the result set, so a full-scan warning is not actionable.
        if (!hasWhere && !hasHaving && !hasGroupBy && !hasLimit && sql.Contains(" FROM "))
            issues.Add(
                new GuardIssue(
                    GuardSeverity.Warning,
                    "NO_FILTER",
                    "Query has no WHERE / HAVING clause or row limit — may perform a full table scan",
                    "Add a WHERE condition to filter rows, or connect a TOP / LIMIT node to cap the result size"
                )
            );
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}
