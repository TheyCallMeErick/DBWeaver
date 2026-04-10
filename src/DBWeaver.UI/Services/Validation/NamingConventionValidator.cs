using System.Text;
using System.Text.RegularExpressions;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Validation.Conventions;
using DBWeaver.UI.ViewModels.Validation.Conventions.Implementations;

namespace DBWeaver.UI.Services.Validation;

/// <summary>
/// Validates node alias names against a <see cref="NamingConventionPolicy"/> and
/// provides an auto-fix helper that converts names to snake_case.
/// </summary>
public static partial class NamingConventionValidator
{
    private static readonly IAliasConventionRegistry DefaultRegistry = AliasConventionRegistry.CreateDefault();

    // ── Regex helpers (source-generated for AOT safety) ──────────────────────

    [GeneratedRegex(@"^[a-z][a-z0-9_]*$")]
    private static partial Regex SnakeCasePattern();

    [GeneratedRegex(@"(?<=[a-z0-9])([A-Z])")]
    private static partial Regex CamelToUnderscorePattern();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphanumPattern();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks a single alias against the policy and returns a list of
    /// (code, message, suggestion) tuples for each violation.
    /// </summary>
    public static IReadOnlyList<(string Code, string Message, string? Suggestion)> CheckAlias(
        string alias,
        NamingConventionPolicy? policy = null
    )
    {
        policy ??= NamingConventionPolicy.Default;
        var violations = new List<(string, string, string?)>();

        if (string.IsNullOrWhiteSpace(alias))
            return violations; // empty alias validated elsewhere (EMPTY_ALIAS)

        // Rule: no spaces
        if (policy.NoSpaces && alias.Contains(' '))
            violations.Add(
                (
                    "NAMING_SPACES",
                    $"Alias '{alias}' contains spaces",
                    $"Use '{ToSnakeCase(alias)}' instead"
                )
            );

        // Rule: leading digit
        if (policy.NoLeadingDigit && alias.Length > 0 && char.IsDigit(alias[0]))
            violations.Add(
                (
                    "NAMING_LEADING_DIGIT",
                    $"Alias '{alias}' starts with a digit",
                    "SQL identifiers must begin with a letter or underscore"
                )
            );

        // Rule: max length
        if (policy.MaxLength > 0 && alias.Length > policy.MaxLength)
            violations.Add(
                (
                    "NAMING_TOO_LONG",
                    $"Alias '{alias}' exceeds maximum length of {policy.MaxLength} characters",
                    $"Shorten to {policy.MaxLength} characters or fewer"
                )
            );

        // Rule: snake_case (only if no-spaces rule did not already flag it,
        // since a space violation is a superset of snake_case violation)
        if (policy.EnforceSnakeCase && !SnakeCasePattern().IsMatch(alias))
        {
            string suggested = ToSnakeCase(alias);
            // Avoid duplicate suggestion when spaces were already flagged
            if (!violations.Any(v => v.Item1 == "NAMING_SPACES"))
                violations.Add(
                    (
                        "NAMING_SNAKE_CASE",
                        $"Alias '{alias}' does not follow snake_case convention",
                        $"Rename to '{suggested}'"
                    )
                );
        }

        return violations;
    }

    /// <summary>
    /// Converts an arbitrary identifier to snake_case.
    /// <para>Examples:</para>
    /// <list type="bullet">
    ///   <item><c>MyAlias</c> → <c>my_alias</c></item>
    ///   <item><c>order Total</c> → <c>order_total</c></item>
    ///   <item><c>HTTPSStatus</c> → <c>https_status</c></item>
    ///   <item><c>123foo</c> → <c>_123foo</c></item>
    /// </list>
    /// </summary>
    public static string ToSnakeCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        // Insert underscore before uppercase runs (camelCase / PascalCase)
        string withUnderscores = CamelToUnderscorePattern().Replace(name, "_$1");

        // Lowercase everything
        string lower = withUnderscores.ToLowerInvariant();

        // Replace any non-alphanumeric character sequences with a single underscore
        string clean = NonAlphanumPattern().Replace(lower, "_").Trim('_');

        // Ensure it does not start with a digit
        if (clean.Length > 0 && char.IsDigit(clean[0]))
            clean = "_" + clean;

        return string.IsNullOrEmpty(clean) ? "alias" : clean;
    }

    /// <summary>
    /// Returns the percentage of nodes with aliases that comply with the given policy.
    /// Returns 100 if no nodes have aliases.
    /// </summary>
    public static int ConformancePercent(
        CanvasViewModel canvas,
        NamingConventionPolicy? policy = null
    )
    {
        policy ??= NamingConventionPolicy.Default;
        var aliasedNodes = canvas.Nodes.Where(n => !string.IsNullOrWhiteSpace(n.Alias)).ToList();

        if (aliasedNodes.Count == 0)
            return 100;

        int compliant = aliasedNodes.Count(n => CheckAlias(n.Alias!, policy).Count == 0);
        return (int)Math.Round(compliant * 100.0 / aliasedNodes.Count);
    }

    public static IReadOnlyList<AliasViolation> CheckAlias(
        string alias,
        NamingConventionPolicy policy,
        IAliasConventionRegistry registry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(registry);

        if (!string.IsNullOrWhiteSpace(policy.ConventionName))
        {
            IAliasConvention convention = registry.Resolve(policy.ConventionName);
            return convention.Check(alias);
        }

        return CheckAlias(alias, policy)
            .Select(v => new AliasViolation(v.Code, v.Message, v.Suggestion))
            .ToList();
    }

    public static string NormalizeAlias(
        string alias,
        NamingConventionPolicy? policy = null,
        IAliasConventionRegistry? registry = null)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return alias;

        policy ??= NamingConventionPolicy.Default;
        registry ??= DefaultRegistry;

        string conventionName = string.IsNullOrWhiteSpace(policy.ConventionName)
            ? "snake_case"
            : policy.ConventionName!;

        IAliasConvention convention = registry.Resolve(conventionName);
        if (convention is IAutoFixableConvention autoFixable)
            return autoFixable.Normalize(alias);

        return ToSnakeCase(alias);
    }

    public static int ConformancePercent(
        CanvasViewModel canvas,
        NamingConventionPolicy policy,
        IAliasConventionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(registry);

        var aliasedNodes = canvas.Nodes.Where(n => !string.IsNullOrWhiteSpace(n.Alias)).ToList();
        if (aliasedNodes.Count == 0)
            return 100;

        int compliant = aliasedNodes.Count(n =>
            CheckAlias(n.Alias!, policy, registry).Count == 0
        );
        return (int)Math.Round(compliant * 100.0 / aliasedNodes.Count);
    }
}
