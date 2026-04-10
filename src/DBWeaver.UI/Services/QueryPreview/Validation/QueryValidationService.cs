
namespace DBWeaver.UI.Services.QueryPreview;

/// <summary>
/// Maps each canvas NodeType to the canonical SqlFn constants it uses during compilation.
/// Only node types that call into ISqlFunctionRegistry are listed.
/// </summary>
internal static class NodeTypeCanonicalFunctions
{
    private static readonly IReadOnlyDictionary<NodeType, IReadOnlyList<string>> Map =
        new Dictionary<NodeType, IReadOnlyList<string>>
        {
            [NodeType.Upper] = [SqlFn.Upper],
            [NodeType.Lower] = [SqlFn.Lower],
            [NodeType.Trim] = [SqlFn.Trim],
            [NodeType.StringLength] = [SqlFn.Length],
            [NodeType.Concat] = [SqlFn.Concat],
            [NodeType.Replace] = [SqlFn.Replace],
            [NodeType.RegexMatch] = [SqlFn.Regex],
            [NodeType.RegexReplace] = [SqlFn.RegexReplace],
            [NodeType.RegexExtract] = [SqlFn.RegexExtract],
            [NodeType.NullFill] = [SqlFn.Coalesce],
            [NodeType.EmptyFill] = [SqlFn.Coalesce, SqlFn.NullIf, SqlFn.Trim],
            [NodeType.JsonExtract] = [SqlFn.JsonExtract],
            [NodeType.JsonValue] = [SqlFn.JsonExtract],
            [NodeType.JsonArrayLength] = [SqlFn.JsonArrayLength],
        };

    public static IEnumerable<string> GetFunctions(IEnumerable<NodeType> types) =>
        types.SelectMany(t => Map.TryGetValue(t, out IReadOnlyList<string>? fns) ? fns : []);
}

/// <summary>
/// Validates SQL queries for portability, mutating commands, and guardrails.
/// Surfaces errors and warnings for the UI to display.
/// </summary>
public sealed class QueryValidationService
{
    private static readonly string[] MutatingKeywords =
    [
        "INSERT",
        "UPDATE",
        "DELETE",
        "DROP",
        "ALTER",
        "TRUNCATE",
        "CREATE",
        "REPLACE",
        "MERGE",
    ];

    /// <summary>
    /// Checks if the SQL contains a data-mutating command (INSERT/UPDATE/DELETE/DROP/ALTER/TRUNCATE).
    /// </summary>
    public static bool IsMutating(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return false;
        string trimmed = sql.TrimStart();

        // Skip leading comments (-- ...\n)
        while (trimmed.StartsWith("--"))
        {
            int nl = trimmed.IndexOf('\n');
            trimmed = nl < 0 ? string.Empty : trimmed[(nl + 1)..].TrimStart();
        }

        foreach (string kw in MutatingKeywords)
        {
            if (trimmed.StartsWith(kw, StringComparison.OrdinalIgnoreCase))
            {
                // Ensure it's a full word (not e.g. "CREATES_TABLE")
                if (trimmed.Length == kw.Length || !char.IsLetterOrDigit(trimmed[kw.Length]))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks portability of functions used in canvas nodes against the target database provider.
    /// Returns a list of unsupported function warnings.
    /// </summary>
    public static List<GuardIssue> CheckPortability(
        IEnumerable<NodeType> usedNodeTypes,
        DatabaseProvider provider
    )
    {
        var registry = new SqlFunctionRegistry(provider);
        IEnumerable<string> usedFunctions = NodeTypeCanonicalFunctions.GetFunctions(usedNodeTypes);
        var issues = new List<GuardIssue>();

        foreach (PortabilityWarning pw in registry.CheckPortability(usedFunctions))
        {
            issues.Add(
                new GuardIssue(
                    GuardSeverity.Block,
                    $"UNSUPPORTED_{pw.FunctionName.Replace(" ", "_").ToUpperInvariant()}",
                    pw.Message,
                    pw.Suggestion
                )
            );
        }

        return issues;
    }
}



