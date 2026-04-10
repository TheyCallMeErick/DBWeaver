using DBWeaver.UI.ViewModels.Validation.Conventions.Implementations;

namespace DBWeaver.UI.ViewModels.Validation.Conventions;

/// <summary>
/// Immutable registry of known alias conventions.
/// </summary>
public sealed class AliasConventionRegistry : IAliasConventionRegistry
{
    private readonly IReadOnlyDictionary<string, IAliasConvention> _conventions;

    public AliasConventionRegistry(IEnumerable<IAliasConvention> conventions)
    {
        ArgumentNullException.ThrowIfNull(conventions);
        _conventions = conventions.ToDictionary(c => c.ConventionName, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<IAliasConvention> All => _conventions.Values.ToList();

    public IAliasConvention? TryResolve(string conventionName) =>
        _conventions.TryGetValue(conventionName, out IAliasConvention? convention)
            ? convention
            : null;

    public IAliasConvention Resolve(string conventionName) =>
        TryResolve(conventionName)
        ?? throw new InvalidOperationException(
            $"Alias convention '{conventionName}' not found. Available: {string.Join(", ", _conventions.Keys)}");

    public static AliasConventionRegistry CreateDefault() =>
        new(
        [
            new SnakeCaseConvention(),
            new CamelCaseConvention(),
            new PascalCaseConvention(),
            new ScreamingSnakeCaseConvention(),
        ]);
}

