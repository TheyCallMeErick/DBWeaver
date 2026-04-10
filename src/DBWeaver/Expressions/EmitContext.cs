using DBWeaver.Core;
using DBWeaver.Registry;

namespace DBWeaver.Expressions;

/// <summary>
/// Passed through every expression during compilation.
/// Carries the provider dialect and the function registry so expressions
/// can produce correct SQL without knowing the database themselves.
/// </summary>
public sealed class EmitContext(DatabaseProvider provider, ISqlFunctionRegistry registry)
{
    private readonly Providers.Dialects.ISqlDialect _dialect =
        new ProviderRegistry(DefaultProviderRegistrations.CreateAll()).GetDialect(provider);

    public DatabaseProvider Provider { get; } = provider;
    public ISqlFunctionRegistry Registry { get; } = registry;

    public EmitContext(DatabaseProvider provider, ISqlFunctionRegistry registry, IProviderRegistry providerRegistry)
        : this(provider, registry)
    {
        ArgumentNullException.ThrowIfNull(providerRegistry);
        _dialect = providerRegistry.GetDialect(provider);
    }

    public string QuoteIdentifier(string id) => _dialect.QuoteIdentifier(id);

    public static string QuoteLiteral(string value) => SqlStringUtility.QuoteLiteral(value);
}
