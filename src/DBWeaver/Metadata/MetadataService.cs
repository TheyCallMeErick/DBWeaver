using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using DBWeaver.Core;

namespace DBWeaver.Metadata;

public sealed class MetadataServiceOptions
{
    public const string CacheTtlSecondsEnvVar = "VSA_METADATA_CACHE_TTL_SECONDS";
    public static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromMinutes(5);

    public TimeSpan CacheTtl { get; set; } = ResolveCacheTtlFromEnvironment();

    internal static TimeSpan ResolveEffectiveCacheTtl(IOptions<MetadataServiceOptions>? options)
    {
        TimeSpan configured = options?.Value.CacheTtl ?? TimeSpan.Zero;
        if (configured > TimeSpan.Zero)
            return configured;

        return ResolveCacheTtlFromEnvironment();
    }

    private static TimeSpan ResolveCacheTtlFromEnvironment()
    {
        string? raw = Environment.GetEnvironmentVariable(CacheTtlSecondsEnvVar);
        if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out int seconds) && seconds > 0)
            return TimeSpan.FromSeconds(seconds);

        return DefaultCacheTtl;
    }
}

/// <summary>
/// Coordinates schema inspection, TTL caching, and auto-join detection.
///
/// Canvas ViewModel lifecycle:
/// <code>
/// // On connect:
/// var meta = await metadataSvc.GetMetadataAsync();
/// treeView.DataContext = meta;
///
/// // On table drag:
/// metadataSvc.AddCanvasTable("orders");
/// var suggestions = await metadataSvc.SuggestJoinsAsync("customers");
/// canvas.ShowJoinGhosts(suggestions);
///
/// // On accept:
/// canvas.MaterialiseJoin(suggestion.ToJoinDefinition());
/// </code>
/// </summary>
public sealed class MetadataService(
    IDatabaseInspector inspector,
    IOptions<MetadataServiceOptions>? options = null,
    ILogger<MetadataService>? logger = null,
    ICanvasTableTracker? canvasTableTracker = null,
    IJoinSuggestionEngine? joinSuggestionEngine = null,
    IMetadataSnapshotCache? snapshotCache = null
) : IDisposable
{
    private readonly IDatabaseInspector _inspector =
        inspector ?? throw new ArgumentNullException(nameof(inspector));
    private readonly ILogger<MetadataService> _logger =
        logger ?? NullLogger<MetadataService>.Instance;
    private readonly ICanvasTableTracker _canvasTableTracker = canvasTableTracker ?? new CanvasTableTracker();
    private readonly IJoinSuggestionEngine _joinSuggestionEngine =
        joinSuggestionEngine ?? new AutoJoinSuggestionEngine();
    private readonly IMetadataSnapshotCache _snapshotCache =
        snapshotCache ?? new MetadataSnapshotCache(MetadataServiceOptions.ResolveEffectiveCacheTtl(options));

    private bool _disposed;

    public static MetadataService Create(
        ConnectionConfig config,
        IOptions<MetadataServiceOptions>? options = null,
        ILogger<MetadataService>? logger = null,
        IDatabaseInspectorFactory? inspectorFactory = null,
        ICanvasTableTracker? canvasTableTracker = null,
        IJoinSuggestionEngine? joinSuggestionEngine = null,
        IMetadataSnapshotCache? snapshotCache = null
    )
    {
        IDatabaseInspectorFactory factory = inspectorFactory ?? DatabaseInspectorFactory.CreateDefault();
        return new(
            factory.Create(config),
            options,
            logger,
            canvasTableTracker,
            joinSuggestionEngine,
            snapshotCache
        );
    }

    // ── Schema retrieval ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the full <see cref="DbMetadata"/> for the TreeView.
    /// Results are cached for <see cref="_cacheTtl"/> and refreshed lazily.
    /// Pass <paramref name="forceRefresh"/> = true after schema migrations.
    /// </summary>
    public async Task<DbMetadata> GetMetadataAsync(
        bool forceRefresh = false,
        CancellationToken ct = default
    )
    {
        return await _snapshotCache.GetOrLoadAsync(
            async loadCt =>
            {
                _logger.LogInformation(
                    "[MetadataService] Introspecting {Provider} — {Db}",
                    _inspector.Provider,
                    "(live)"
                );

                var sw = System.Diagnostics.Stopwatch.StartNew();
                DbMetadata metadata = await _inspector.InspectAsync(loadCt);
                sw.Stop();

                _logger.LogInformation(
                    "[MetadataService] Done — {T} tables · {V} views · {FK} FKs in {Ms}ms",
                    metadata.TotalTables,
                    metadata.TotalViews,
                    metadata.TotalForeignKeys,
                    sw.ElapsedMilliseconds
                );

                return metadata;
            },
            forceRefresh,
            ct
        );
    }

    // ── Single-table refresh ──────────────────────────────────────────────────

    /// <summary>
    /// Re-introspects one table in isolation and hot-swaps it in the cache.
    /// Canvas nodes call this after an ALTER TABLE.
    /// </summary>
    public async Task<TableMetadata> RefreshTableAsync(
        string schema,
        string table,
        CancellationToken ct = default
    )
    {
        TableMetadata fresh = await _inspector.InspectTableAsync(schema, table, ct);

        _snapshotCache.ReplaceTable(fresh);

        _logger.LogDebug("[MetadataService] Refreshed table {S}.{T}", schema, table);
        return fresh;
    }

    // ── FK fast-path ──────────────────────────────────────────────────────────

    /// <summary>Returns all FK relations — fast path for arrow-drawing on the canvas.</summary>
    public async Task<IReadOnlyList<ForeignKeyRelation>> GetForeignKeysAsync(
        CancellationToken ct = default
    ) => (await GetMetadataAsync(ct: ct)).AllForeignKeys;

    // ── Canvas table tracking ─────────────────────────────────────────────────

    /// <summary>Register a table that was placed on the canvas.</summary>
    public void AddCanvasTable(string fullTableName)
    {
        _canvasTableTracker.Add(fullTableName);
        _logger.LogDebug(
            "[Canvas] Added '{T}' — canvas size: {N}",
            fullTableName,
            _canvasTableTracker.Count
        );
    }

    /// <summary>Remove a table that was deleted from the canvas.</summary>
    public void RemoveCanvasTable(string fullTableName)
    {
        _canvasTableTracker.Remove(fullTableName);
        _logger.LogDebug(
            "[Canvas] Removed '{T}' — canvas size: {N}",
            fullTableName,
            _canvasTableTracker.Count
        );
    }

    /// <summary>Returns the tables currently on the canvas (snapshot, thread-safe).</summary>
    public IReadOnlyList<string> CanvasTables => _canvasTableTracker.Snapshot();

    // ── Auto-Join (main canvas entry point) ───────────────────────────────────

    /// <summary>
    /// Called by the ViewModel immediately after <paramref name="newTable"/> is dropped.
    /// Uses the internally tracked canvas set.
    ///
    /// The canvas should render ghost JOIN nodes for high-confidence suggestions
    /// (score ≥ 0.85) immediately, and show lesser ones in a suggestion panel.
    /// </summary>
    public async Task<IReadOnlyList<JoinSuggestion>> SuggestJoinsAsync(
        string newTable,
        CancellationToken ct = default
    ) => await SuggestJoinsAsync(newTable, _canvasTableTracker.Snapshot(), ct);

    /// <summary>
    /// Stateless overload — caller supplies the canvas table set.
    /// Preferred in unit tests and ViewModels that own their own state.
    /// </summary>
    public async Task<IReadOnlyList<JoinSuggestion>> SuggestJoinsAsync(
        string newTable,
        IEnumerable<string> canvasTables,
        CancellationToken ct = default
    )
    {
        DbMetadata metadata = await GetMetadataAsync(ct: ct);
        IReadOnlyList<JoinSuggestion> suggestions = _joinSuggestionEngine.Suggest(
            metadata,
            newTable,
            canvasTables
        );

        _logger.LogInformation(
            "[AutoJoin] '{New}' → {N} suggestion(s): [{Pairs}]",
            newTable,
            suggestions.Count,
            string.Join(
                ", ",
                suggestions.Select(s => $"{s.ExistingTable}↔{s.NewTable}({s.Score:P0})")
            )
        );

        return suggestions;
    }

    // ── Cache control ─────────────────────────────────────────────────────────

    public void InvalidateCache()
    {
        _snapshotCache.Invalidate();
        _logger.LogDebug("[MetadataService] Cache invalidated.");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _snapshotCache.Dispose();
        _disposed = true;
    }
}
