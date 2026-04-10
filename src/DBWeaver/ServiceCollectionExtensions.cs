using Microsoft.Extensions.DependencyInjection;
using DBWeaver.Metadata;
using DBWeaver.QueryEngine;
using DBWeaver.Registry;

namespace DBWeaver;

public sealed class DBWeaverServiceOptions
{
    public IEnumerable<IProviderRegistration>? ProviderRegistrations { get; set; }
    public IEnumerable<OrchestratorRegistration>? OrchestratorRegistrations { get; set; }
    public IEnumerable<InspectorRegistration>? InspectorRegistrations { get; set; }
    public Func<ICanvasTableTracker>? CanvasTableTrackerFactory { get; set; }
}

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all DBWeaver services.
    /// Call this in Avalonia's App.axaml.cs or the composition root.
    ///
    /// <code>
    /// services.AddDBWeaver();
    /// </code>
    /// </summary>
    public static IServiceCollection AddDBWeaver(this IServiceCollection services) =>
        AddDBWeaver(services, configure: null);

    /// <summary>
    /// Registers all DBWeaver services with optional overrides for default registrations.
    /// </summary>
    public static IServiceCollection AddDBWeaver(
        this IServiceCollection services,
        Action<DBWeaverServiceOptions>? configure
    )
    {
        var options = new DBWeaverServiceOptions();
        configure?.Invoke(options);

        IEnumerable<IProviderRegistration> providerRegistrations =
            options.ProviderRegistrations ?? DefaultProviderRegistrations.CreateAll();
        IEnumerable<OrchestratorRegistration> orchestratorRegistrations =
            options.OrchestratorRegistrations ?? DbOrchestratorFactory.CreateDefaultRegistrations();
        IEnumerable<InspectorRegistration> inspectorRegistrations =
            options.InspectorRegistrations ?? DatabaseInspectorFactory.CreateDefaultRegistrations();
        Func<ICanvasTableTracker> canvasTableTrackerFactory =
            options.CanvasTableTrackerFactory ?? (() => new CanvasTableTracker());

        services.AddSingleton<IProviderRegistry>(
            _ => new ProviderRegistry(providerRegistrations)
        );
        services.AddSingleton<IDatabaseInspectorFactory>(_ => new DatabaseInspectorFactory(inspectorRegistrations));
        services.AddSingleton<ICanvasTableTracker>(_ => canvasTableTrackerFactory());
        services.AddSingleton<IDbOrchestratorFactory>(_ => new DbOrchestratorFactory(orchestratorRegistrations));

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
