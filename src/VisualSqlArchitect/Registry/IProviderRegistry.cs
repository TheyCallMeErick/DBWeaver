using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata;
using VisualSqlArchitect.Providers.Dialects;
using VisualSqlArchitect.QueryEngine;

namespace VisualSqlArchitect.Registry;

/// <summary>
/// Central registry for managing database providers and their dependencies.
/// Provides factory methods for creating provider-specific components.
/// </summary>
public interface IProviderRegistry
{
    /// <summary>
    /// Registers a provider with its dialect, metadata queries, and function fragments.
    /// </summary>
    void RegisterProvider(
        DatabaseProvider provider,
        ISqlDialect dialect,
        IMetadataQueryProvider metadataProvider,
        IFunctionFragmentProvider functionFragments
    );

    /// <summary>
    /// Creates a SqlFunctionRegistry for the specified provider.
    /// </summary>
    ISqlFunctionRegistry CreateFunctionRegistry(DatabaseProvider provider);

    /// <summary>
    /// Creates a QueryBuilderService for the specified provider and table.
    /// </summary>
    QueryBuilderService CreateQueryBuilder(DatabaseProvider provider, string fromTable);

    /// <summary>
    /// Gets the dialect for the specified provider.
    /// </summary>
    ISqlDialect GetDialect(DatabaseProvider provider);

    /// <summary>
    /// Gets the metadata provider for the specified provider.
    /// </summary>
    IMetadataQueryProvider GetMetadataProvider(DatabaseProvider provider);

    /// <summary>
    /// Gets the function fragments for the specified provider.
    /// </summary>
    IFunctionFragmentProvider GetFunctionFragments(DatabaseProvider provider);

    /// <summary>
    /// Checks if a provider is registered.
    /// </summary>
    bool IsProviderRegistered(DatabaseProvider provider);

    /// <summary>
    /// Gets all registered providers.
    /// </summary>
    IReadOnlyList<DatabaseProvider> GetRegisteredProviders();
}
