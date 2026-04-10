
namespace DBWeaver.UI.Services.QueryPreview;

public static class PreviewDiagnosticMapper
{
    private sealed record Rule(
        Regex Pattern,
        PreviewDiagnosticCategory Category,
        PreviewDiagnosticSeverity Severity,
        string Code
    );

    private static readonly IReadOnlyList<Rule> Rules =
    [
        new(
            new Regex("cycle detected|recursive", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            PreviewDiagnosticCategory.Cte,
            PreviewDiagnosticSeverity.Error,
            "E-CTE-001"
        ),
        new(
            new Regex("\\bcte\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            PreviewDiagnosticCategory.Cte,
            PreviewDiagnosticSeverity.Warning,
            "W-CTE-001"
        ),
        new(
            new Regex("\\bwindow\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            PreviewDiagnosticCategory.Window,
            PreviewDiagnosticSeverity.Warning,
            "W-WIN-001"
        ),
        new(
            new Regex("\\bjoin\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            PreviewDiagnosticCategory.Join,
            PreviewDiagnosticSeverity.Warning,
            "W-JOIN-001"
        ),
        new(
            new Regex("\\bsubquery\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            PreviewDiagnosticCategory.Subquery,
            PreviewDiagnosticSeverity.Warning,
            "W-SUB-001"
        ),
        new(
            new Regex("type mismatch|incompatible|pin type", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            PreviewDiagnosticCategory.TypeCompatibility,
            PreviewDiagnosticSeverity.Warning,
            "W-TYP-001"
        ),
        new(
            new Regex("comparison|between|like", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            PreviewDiagnosticCategory.Comparison,
            PreviewDiagnosticSeverity.Warning,
            "W-CMP-001"
        ),
        new(
            new Regex("where|having|predicate|logic", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            PreviewDiagnosticCategory.Predicate,
            PreviewDiagnosticSeverity.Warning,
            "W-PRD-001"
        ),
    ];

    public static List<PreviewDiagnostic> FromLegacyMessages(IEnumerable<string> messages)
    {
        return messages
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(FromLegacyMessage)
            .ToList();
    }

    public static PreviewDiagnostic FromLegacyMessage(string message)
    {
        foreach (Rule rule in Rules)
        {
            if (!rule.Pattern.IsMatch(message))
                continue;

            PreviewDiagnosticSeverity severity = UpgradeSeverityIfNeeded(message, rule.Severity);
            string code = severity == PreviewDiagnosticSeverity.Error && rule.Code.StartsWith("W-", StringComparison.Ordinal)
                ? "E-" + rule.Code[2..]
                : rule.Code;

            return new PreviewDiagnostic(severity, rule.Category, code, message);
        }

        PreviewDiagnosticSeverity fallbackSeverity = InferFallbackSeverity(message);
        string fallbackCode = fallbackSeverity == PreviewDiagnosticSeverity.Error ? "E-GEN-001" : "W-GEN-001";
        return new PreviewDiagnostic(fallbackSeverity, PreviewDiagnosticCategory.General, fallbackCode, message);
    }

    private static PreviewDiagnosticSeverity UpgradeSeverityIfNeeded(string message, PreviewDiagnosticSeverity current)
    {
        PreviewDiagnosticSeverity inferred = InferFallbackSeverity(message);
        return inferred > current ? inferred : current;
    }

    private static PreviewDiagnosticSeverity InferFallbackSeverity(string message)
    {
        if (message.StartsWith("info:", StringComparison.OrdinalIgnoreCase))
            return PreviewDiagnosticSeverity.Info;

        if (message.Contains("missing required", StringComparison.OrdinalIgnoreCase)
            || message.Contains("invalid", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cycle detected", StringComparison.OrdinalIgnoreCase)
            || message.Contains("requires", StringComparison.OrdinalIgnoreCase)
            || message.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            return PreviewDiagnosticSeverity.Error;
        }

        return PreviewDiagnosticSeverity.Warning;
    }
}


