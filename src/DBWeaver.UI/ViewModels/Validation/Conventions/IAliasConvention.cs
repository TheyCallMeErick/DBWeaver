namespace DBWeaver.UI.ViewModels.Validation.Conventions;

/// <summary>
/// Naming convention contract for SQL aliases.
/// Implementations should be stateless and thread-safe.
/// </summary>
public interface IAliasConvention
{
    /// <summary>Canonical convention name (for example: snake_case, camelCase).</summary>
    string ConventionName { get; }

    /// <summary>Checks the alias and returns all violations.</summary>
    IReadOnlyList<AliasViolation> Check(string alias);

    bool IsCompliant(string alias) => Check(alias).Count == 0;
}

