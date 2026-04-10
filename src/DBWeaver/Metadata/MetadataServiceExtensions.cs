using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DBWeaver.Core;

namespace DBWeaver.Metadata;

public sealed class MetadataIntelligenceOptions
{
    public Func<IServiceProvider, IJoinSuggestionEngine>? JoinSuggestionEngineFactory { get; set; }
    public Func<IServiceProvider, IMetadataSnapshotCache>? SnapshotCacheFactory { get; set; }
}

public static class MetadataServiceExtensions
{
    /// <summary>
    /// Registers the full metadata intelligence stack.
    /// Call after <c>services.AddDBWeaver()</c>.
    /// </summary>
    public static IServiceCollection AddMetadataIntelligence(
        this IServiceCollection services,
        Action<MetadataServiceOptions>? configure = null
    ) => AddMetadataIntelligence(services, configure, configureIntelligence: null);

    /// <summary>
    /// Registers metadata intelligence with optional composition overrides for internals.
    /// </summary>
    public static IServiceCollection AddMetadataIntelligence(
        this IServiceCollection services,
        Action<MetadataServiceOptions>? configure,
        Action<MetadataIntelligenceOptions>? configureIntelligence
    )
    {
        var intelligenceOptions = new MetadataIntelligenceOptions();
        configureIntelligence?.Invoke(intelligenceOptions);

        if (configure is not null)
            services.Configure(configure);
        else
            services.AddOptions<MetadataServiceOptions>();

        if (intelligenceOptions.JoinSuggestionEngineFactory is not null)
            services.AddSingleton(intelligenceOptions.JoinSuggestionEngineFactory);
        else
            services.AddSingleton<IJoinSuggestionEngine, AutoJoinSuggestionEngine>();

        services.AddSingleton(sp =>
        {
            ActiveConnectionContext ctx = sp.GetRequiredService<ActiveConnectionContext>();
            ILogger<MetadataService>? logger = sp.GetService<ILogger<MetadataService>>();
            IOptions<MetadataServiceOptions>? options = sp.GetService<IOptions<MetadataServiceOptions>>();
            IDatabaseInspectorFactory? inspectorFactory = sp.GetService<IDatabaseInspectorFactory>();
            ICanvasTableTracker? canvasTableTracker = sp.GetService<ICanvasTableTracker>();
            IJoinSuggestionEngine? joinSuggestionEngine = sp.GetService<IJoinSuggestionEngine>();
            IMetadataSnapshotCache? snapshotCache = intelligenceOptions.SnapshotCacheFactory?.Invoke(sp);

            // Config is available after SwitchAsync(); service is lazily initialised.
            ConnectionConfig config =
                ctx.Config ?? throw new InvalidOperationException("No active connection configured");

            return MetadataService.Create(
                config,
                options,
                logger,
                inspectorFactory,
                canvasTableTracker,
                joinSuggestionEngine,
                snapshotCache
            );
        });

        return services;
    }
}
