using Microsoft.Extensions.DependencyInjection;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Providers;
using VisualSqlArchitect.QueryEngine;
using VisualSqlArchitect.Registry;

namespace VisualSqlArchitect;

// ─── Orchestrator Factory ─────────────────────────────────────────────────────

/// <summary>
/// Single creation point for <see cref="IDbOrchestrator"/> instances.
/// The canvas passes a <see cref="ConnectionConfig"/> when the user configures
/// a new data-source node; the factory resolves the correct implementation.
/// </summary>
public static class DbOrchestratorFactory
{
    public static IDbOrchestrator Create(ConnectionConfig config) =>
        config.Provider switch
        {
            DatabaseProvider.SqlServer => new SqlServerOrchestrator(config),
            DatabaseProvider.MySql => new MySqlOrchestrator(config),
            DatabaseProvider.Postgres => new PostgresOrchestrator(config),
            DatabaseProvider.SQLite => new SqliteOrchestrator(config),
            _ => throw new NotSupportedException($"Provider '{config.Provider}' is not supported."),
        };
}

// ─── Active Connection Context ────────────────────────────────────────────────

/// <summary>
/// Holds the live orchestrator, function registry and query builder for the
/// currently active connection in the canvas session.
///
/// Swap <see cref="SwitchAsync"/> when the user changes data-source nodes.
/// </summary>
public sealed class ActiveConnectionContext : IAsyncDisposable
{
    private IDbOrchestrator? _orchestrator;
    private ConnectionConfig? _config;
    private readonly IProviderRegistry _providerRegistry = ProviderRegistry.CreateDefault();

    public IDbOrchestrator Orchestrator =>
        _orchestrator
        ?? throw new InvalidOperationException("No active connection. Call SwitchAsync() first.");

    public ISqlFunctionRegistry FunctionRegistry { get; private set; } =
        new SqlFunctionRegistry(DatabaseProvider.Postgres); // safe default

    public QueryBuilderService QueryBuilder { get; private set; } =
        QueryBuilderService.Create(DatabaseProvider.Postgres, "");

    public DatabaseProvider Provider => _orchestrator?.Provider ?? DatabaseProvider.Postgres;

    public ConnectionConfig? Config => _config;

    /// <summary>
    /// Replaces the active connection.  Disposes the previous orchestrator
    /// gracefully before switching.
    /// </summary>
    public async Task SwitchAsync(ConnectionConfig config, CancellationToken ct = default)
    {
        if (_orchestrator is not null)
            await _orchestrator.DisposeAsync();

        _config = config;
        _orchestrator = DbOrchestratorFactory.Create(config);

        // Use IProviderRegistry to create components with all dependencies
        FunctionRegistry = _providerRegistry.CreateFunctionRegistry(config.Provider);
        QueryBuilder = _providerRegistry.CreateQueryBuilder(config.Provider, "");

        // Eagerly validate so the canvas shows a connection error immediately
        ConnectionTestResult test = await _orchestrator.TestConnectionAsync(ct);
        if (!test.Success)
            throw new InvalidOperationException($"Connection failed: {test.ErrorMessage}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_orchestrator is not null)
            await _orchestrator.DisposeAsync();
    }
}

// ─── DI Extensions ───────────────────────────────────────────────────────────

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Visual SQL Architect services.
    /// Call this in Avalonia's App.axaml.cs or the composition root.
    ///
    /// <code>
    /// services.AddVisualSqlArchitect();
    /// </code>
    /// </summary>
    public static IServiceCollection AddVisualSqlArchitect(this IServiceCollection services)
    {
        // ActiveConnectionContext is a singleton so the canvas always shares
        // the same live connection across all view-models.
        services.AddSingleton<ActiveConnectionContext>();

        // FunctionRegistry and QueryBuilder are resolved from the context;
        // register factory delegates so VMs can request the current instance.
        services.AddTransient<ISqlFunctionRegistry>(sp =>
            sp.GetRequiredService<ActiveConnectionContext>().FunctionRegistry
        );

        services.AddTransient<QueryBuilderService>(sp =>
            sp.GetRequiredService<ActiveConnectionContext>().QueryBuilder
        );

        return services;
    }
}

