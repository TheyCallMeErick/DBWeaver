using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata;
using VisualSqlArchitect.Providers.Dialects;
using VisualSqlArchitect.QueryEngine;
using VisualSqlArchitect.Registry;

namespace VisualSqlArchitect.Tests.Unit.Registry;

/// <summary>
/// Tests for IProviderRegistry and ProviderRegistry implementation.
/// Validates central provider management and factory methods.
/// </summary>
public class ProviderRegistryTests
{
    [Fact]
    public void CreateDefault_RegistersAllProviders()
    {
        // Arrange & Act
        var registry = ProviderRegistry.CreateDefault() as ProviderRegistry;

        // Assert
        Assert.NotNull(registry);
        Assert.True(registry.IsProviderRegistered(DatabaseProvider.Postgres));
        Assert.True(registry.IsProviderRegistered(DatabaseProvider.MySql));
        Assert.True(registry.IsProviderRegistered(DatabaseProvider.SqlServer));
    }

    [Theory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void GetDialect_ReturnsRegisteredDialect(DatabaseProvider provider)
    {
        // Arrange
        var registry = ProviderRegistry.CreateDefault();

        // Act
        var dialect = registry.GetDialect(provider);

        // Assert
        Assert.NotNull(dialect);
    }

    [Theory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void GetMetadataProvider_ReturnsRegisteredProvider(DatabaseProvider provider)
    {
        // Arrange
        var registry = ProviderRegistry.CreateDefault();

        // Act
        var metadataProvider = registry.GetMetadataProvider(provider);

        // Assert
        Assert.NotNull(metadataProvider);
    }

    [Theory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void GetFunctionFragments_ReturnsRegisteredFragments(DatabaseProvider provider)
    {
        // Arrange
        var registry = ProviderRegistry.CreateDefault();

        // Act
        var fragments = registry.GetFunctionFragments(provider);

        // Assert
        Assert.NotNull(fragments);
    }

    [Theory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void CreateFunctionRegistry_ReturnsFunctionRegistry(DatabaseProvider provider)
    {
        // Arrange
        var registry = ProviderRegistry.CreateDefault();

        // Act
        var fnRegistry = registry.CreateFunctionRegistry(provider);

        // Assert
        Assert.NotNull(fnRegistry);
        Assert.IsAssignableFrom<ISqlFunctionRegistry>(fnRegistry);
    }

    [Theory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void CreateQueryBuilder_ReturnsQueryBuilder(DatabaseProvider provider)
    {
        // Arrange
        var registry = ProviderRegistry.CreateDefault();

        // Act
        var builder = registry.CreateQueryBuilder(provider, "test_table");

        // Assert
        Assert.NotNull(builder);
        Assert.IsAssignableFrom<QueryBuilderService>(builder);
    }

    [Fact]
    public void CreateFunctionRegistry_CachesInstances()
    {
        // Arrange
        var registry = ProviderRegistry.CreateDefault();

        // Act
        var fnRegistry1 = registry.CreateFunctionRegistry(DatabaseProvider.Postgres);
        var fnRegistry2 = registry.CreateFunctionRegistry(DatabaseProvider.Postgres);

        // Assert - Should return same instance (cached)
        Assert.Same(fnRegistry1, fnRegistry2);
    }

    [Fact]
    public void GetRegisteredProviders_ReturnsAllProviders()
    {
        // Arrange
        var registry = ProviderRegistry.CreateDefault();

        // Act
        var providers = registry.GetRegisteredProviders();

        // Assert
        Assert.NotEmpty(providers);
        Assert.Contains(DatabaseProvider.Postgres, providers);
        Assert.Contains(DatabaseProvider.MySql, providers);
        Assert.Contains(DatabaseProvider.SqlServer, providers);
    }

    [Fact]
    public void RegisterProvider_ThrowsOnNullDialect()
    {
        // Arrange
        var registry = new ProviderRegistry();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            registry.RegisterProvider(
                DatabaseProvider.Postgres,
                null!,
                new PostgresMetadataQueries(),
                new PostgresFunctionFragments()
            )
        );
    }

    [Fact]
    public void GetDialect_ThrowsOnUnregisteredProvider()
    {
        // Arrange
        var registry = new ProviderRegistry();

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => registry.GetDialect(DatabaseProvider.Postgres));
    }
}
