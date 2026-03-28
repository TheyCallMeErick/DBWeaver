using System.Text.RegularExpressions;

namespace VisualSqlArchitect.UI.ViewModels;

// ── Error model ───────────────────────────────────────────────────────────────

public enum ErrorCategory
{
    Connection,
    Authorization,
    Syntax,
    Schema,
    Timeout,
    Compatibility,
    SafePreview,
    Unknown,
}

public sealed record DiagnosticResult(
    ErrorCategory Category,
    string CategoryLabel,
    string CategoryIcon,
    string FriendlyMessage,
    string? TechnicalDetail,
    string Suggestion
);

// ── Classifier ────────────────────────────────────────────────────────────────

/// <summary>
/// Classifies raw error strings into structured <see cref="DiagnosticResult"/>
/// records with actionable suggestions. Sanitizes connection strings and
/// credentials before exposing technical details.
/// </summary>
public static partial class ErrorDiagnostics
{
    // Patterns that reveal credentials — strip them from technical details
    private static readonly Regex[] SanitizePatterns =
    [
        MyRegex(),
        new(@"(User\s*Id|uid|user)\s*=\s*[^;'""]+", RegexOptions.IgnoreCase),
        new(@"Server\s*=\s*[^;'""]+", RegexOptions.IgnoreCase),
        new(@"Data\s+Source\s*=\s*[^;'""]+", RegexOptions.IgnoreCase),
        new(@"Host\s*=\s*[^;'""]+", RegexOptions.IgnoreCase),
    ];

    public static DiagnosticResult Classify(string rawMessage, Exception? exception = null)
    {
        string msg = rawMessage ?? string.Empty;
        string lower = msg.ToLowerInvariant();
        string sanitized = Sanitize(msg);

        // ── Safe Preview block (our own messages) ─────────────────────────────
        if (
            lower.Contains("safe preview")
            || lower.Contains("mutating command")
            || lower.Contains("blocked in")
        )
            return Make(
                ErrorCategory.SafePreview,
                NodeIconCatalog.DiagSafePreview,
                "Blocked by Safe Preview Mode",
                "This SQL contains a data-mutating command and cannot be executed in preview.",
                sanitized,
                "Remove or replace the mutating command (INSERT / UPDATE / DELETE / DROP / ALTER / TRUNCATE) before running preview."
            );

        // ── Connection errors ─────────────────────────────────────────────────
        if (
            lower.ContainsAny(
                "connection refused",
                "connection timed out",
                "no route to host",
                "network is unreachable",
                "unable to connect",
                "host not found",
                "name or service not known",
                "econnrefused"
            )
        )
            return Make(
                ErrorCategory.Connection,
                NodeIconCatalog.DiagConnection,
                "Connection failed",
                "Could not reach the database server. The host may be down, unreachable, or blocking connections.",
                sanitized,
                "Verify the server address and port, ensure the database is running, and check firewall rules."
            );

        // ── Authorization errors ───────────────────────────────────────────────
        if (
            lower.ContainsAny(
                "permission denied",
                "access denied",
                "insufficient privilege",
                "unauthorized",
                "authentication failed",
                "password authentication",
                "login failed",
                "not authorized",
                "privilege"
            )
        )
            return Make(
                ErrorCategory.Authorization,
                NodeIconCatalog.DiagAuthorization,
                "Authorization error",
                "The current credentials do not have permission to perform this operation.",
                sanitized,
                "Confirm the database user has SELECT privileges on the target table/schema, or contact your DBA."
            );

        // ── Timeout errors ────────────────────────────────────────────────────
        if (
            lower.ContainsAny(
                "timeout",
                "timed out",
                "query was cancelled",
                "cancellation",
                "statement timeout",
                "lock timeout",
                "deadlock"
            )
        )
            return Make(
                ErrorCategory.Timeout,
                NodeIconCatalog.DiagTimeout,
                "Query timeout",
                "The query took too long to complete and was cancelled by the server or client.",
                sanitized,
                "Add a WHERE clause or LIMIT to reduce the result set, or increase the query timeout in connection settings."
            );

        // ── Schema errors ─────────────────────────────────────────────────────
        if (
            lower.ContainsAny("column", "table", "relation", "object", "view", "schema")
            && lower.ContainsAny(
                "not exist",
                "not found",
                "does not exist",
                "unknown column",
                "invalid column",
                "invalid object name",
                "no such table",
                "no such column"
            )
        )
            return Make(
                ErrorCategory.Schema,
                NodeIconCatalog.DiagSchema,
                "Schema error",
                "A referenced table, column, or object could not be found in the database.",
                sanitized,
                "Check that all table/column names are spelled correctly and that the schema matches the active connection."
            );

        // ── Syntax errors ─────────────────────────────────────────────────────
        if (
            lower.ContainsAny(
                "syntax error",
                "unexpected token",
                "parse error",
                "near \"",
                "sql syntax",
                "incorrect syntax",
                "invalid syntax",
                "unterminated",
                "unbalanced"
            )
        )
            return Make(
                ErrorCategory.Syntax,
                NodeIconCatalog.DiagSyntax,
                "SQL syntax error",
                "The query contains a syntax error and could not be parsed by the database engine.",
                sanitized,
                "Review the highlighted SQL for typos, mismatched parentheses, or unsupported clauses for the active provider."
            );

        // ── Compatibility / function errors ───────────────────────────────────
        if (
            lower.ContainsAny("function", "procedure", "operator")
            && lower.ContainsAny(
                "not exist",
                "not supported",
                "does not support",
                "undefined function",
                "unknown function",
                "no function matches"
            )
        )
            return Make(
                ErrorCategory.Compatibility,
                NodeIconCatalog.DiagCompatibility,
                "Compatibility error",
                "A function, operator, or syntax construct is not supported by the active database provider.",
                sanitized,
                "Switch to the correct provider in the SQL bar, or replace the unsupported construct with an equivalent."
            );

        // ── Fallback ──────────────────────────────────────────────────────────
        return Make(
            ErrorCategory.Unknown,
            NodeIconCatalog.DiagUnknown,
            "Unexpected error",
            "An error occurred while running the preview query.",
            sanitized,
            "Check the technical details below and verify that your canvas is configured correctly."
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DiagnosticResult Make(
        ErrorCategory cat,
        string icon,
        string label,
        string friendly,
        string? technical,
        string suggestion
    ) => new(cat, label, icon, friendly, technical, suggestion);

    private static string Sanitize(string msg)
    {
        foreach (Regex rx in SanitizePatterns)
            msg = rx.Replace(msg, m => m.Value.Split('=')[0] + "=***");
        return msg;
    }

    [GeneratedRegex(
        @"(Password|Pwd|password|pwd)\s*=\s*[^;'""]+",
        RegexOptions.IgnoreCase,
        "pt-BR"
    )]
    private static partial Regex MyRegex();
}

// ── Extension helpers ─────────────────────────────────────────────────────────

file static class StringExtensions
{
    public static bool ContainsAny(this string source, params string[] values)
    {
        foreach (string v in values)
            if (source.Contains(v, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
