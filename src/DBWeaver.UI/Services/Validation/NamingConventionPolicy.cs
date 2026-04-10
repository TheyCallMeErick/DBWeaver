namespace DBWeaver.UI.Services.Validation;

/// <summary>
/// Configurable policy that defines the naming rules applied to node aliases
/// and other user-supplied identifiers in the SQL graph.
/// </summary>
public sealed class NamingConventionPolicy
{
    // ── Rules ────────────────────────────────────────────────────────────────

    /// <summary>
    /// When <c>true</c>, aliases must use snake_case (all lowercase, words
    /// separated by underscores, no spaces or mixed-case letters).
    /// </summary>
    public bool EnforceSnakeCase { get; init; } = true;

    /// <summary>Maximum allowed length for an alias (0 = unlimited).</summary>
    public int MaxLength { get; init; } = 64;

    /// <summary>When <c>true</c>, aliases must not start with a digit.</summary>
    public bool NoLeadingDigit { get; init; } = true;

    /// <summary>
    /// When <c>true</c>, aliases must not contain spaces (applies regardless
    /// of <see cref="EnforceSnakeCase"/>).
    /// </summary>
    public bool NoSpaces { get; init; } = true;

    /// <summary>
    /// Canonical convention name used by convention-based validators.
    /// When set, convention-based path takes precedence over legacy flags.
    /// </summary>
    public string? ConventionName { get; init; } = "snake_case";

    // ── Singleton defaults ────────────────────────────────────────────────────

    /// <summary>Default policy applied to all graphs when no override is set.</summary>
    public static NamingConventionPolicy Default { get; } = new();

    /// <summary>Relaxed policy that only enforces no-spaces and max-length.</summary>
    public static NamingConventionPolicy Relaxed { get; } =
        new()
        {
            EnforceSnakeCase = false,
            MaxLength = 128,
            NoLeadingDigit = false,
            NoSpaces = true,
            ConventionName = null,
        };
}
