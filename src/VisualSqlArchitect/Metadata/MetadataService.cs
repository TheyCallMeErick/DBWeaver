using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata.Inspectors;

namespace VisualSqlArchitect.Metadata;

// ─── Inspector factory ────────────────────────────────────────────────────────

public static class InspectorFactory
{
    public static IDatabaseInspector Create(ConnectionConfig config) => config.Provider switch
    {
        DatabaseProvider.SqlServer => new SqlServerInspector(config),
        DatabaseProvider.MySql     => new MySqlInspector(config),
        DatabaseProvider.Postgres  => new PostgresInspector(config),
        _ => throw new NotSupportedException($"No inspector for '{config.Provider}'.")
    };
}

// ─── Cache entry ──────────────────────────────────────────────────────────────

internal record CacheEntry(DbMetadata Metadata, DateTimeOffset ExpiresAt);

// ─── Thread-safe HashSet wrapper ─────────────────────────────────────────────

internal sealed class ConcurrentStringSet
{
    private readonly HashSet<string> _inner = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public void Add(string item)    { lock (_lock) _inner.Add(item); }
    public void Remove(string item) { lock (_lock) _inner.Remove(item); }
    public bool Contains(string item) { lock (_lock) return _inner.Contains(item); }
    public IReadOnlyList<string> Snapshot() { lock (_lock) return _inner.ToList(); }
    public int Count { get { lock (_lock) return _inner.Count; } }
}

// ─── Service ──────────────────────────────────────────────────────────────────

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
public sealed class MetadataService
{
    private readonly IDatabaseInspector _inspector;
    private readonly ILogger<MetadataService> _logger;
    private readonly TimeSpan _cacheTtl;

    private volatile CacheEntry? _cache;
    private readonly SemaphoreSlim _fetchLock = new(1, 1);
    private readonly ConcurrentStringSet _canvasTables = new();

    public MetadataService(
        IDatabaseInspector inspector,
        TimeSpan? cacheTtl = null,
        ILogger<MetadataService>? logger = null)
    {
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        _cacheTtl  = cacheTtl ?? TimeSpan.FromMinutes(5);
        _logger    = logger ?? NullLogger<MetadataService>.Instance;
    }

    public static MetadataService Create(
        ConnectionConfig config,
        TimeSpan? cacheTtl = null,
        ILogger<MetadataService>? logger = null) =>
        new(InspectorFactory.Create(config), cacheTtl, logger);

    // ── Schema retrieval ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the full <see cref="DbMetadata"/> for the TreeView.
    /// Results are cached for <see cref="_cacheTtl"/> and refreshed lazily.
    /// Pass <paramref name="forceRefresh"/> = true after schema migrations.
    /// </summary>
    public async Task<DbMetadata> GetMetadataAsync(
        bool forceRefresh = false, CancellationToken ct = default)
    {
        // Fast path — check outside the lock first
        var cached = _cache;
        if (!forceRefresh && IsFresh(cached))
            return cached!.Metadata;

        await _fetchLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring
            cached = _cache;
            if (!forceRefresh && IsFresh(cached))
                return cached!.Metadata;

            _logger.LogInformation(
                "[MetadataService] Introspecting {Provider} — {Db}",
                _inspector.Provider, "(live)");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var metadata = await _inspector.InspectAsync(ct);
            sw.Stop();

            _logger.LogInformation(
                "[MetadataService] Done — {T} tables · {V} views · {FK} FKs in {Ms}ms",
                metadata.TotalTables, metadata.TotalViews,
                metadata.TotalForeignKeys, sw.ElapsedMilliseconds);

            _cache = new CacheEntry(metadata, DateTimeOffset.UtcNow.Add(_cacheTtl));
            return metadata;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    private static bool IsFresh(CacheEntry? entry) =>
        entry is not null && DateTimeOffset.UtcNow < entry.ExpiresAt;

    // ── Single-table refresh ──────────────────────────────────────────────────

    /// <summary>
    /// Re-introspects one table in isolation and hot-swaps it in the cache.
    /// Canvas nodes call this after an ALTER TABLE.
    /// </summary>
    public async Task<TableMetadata> RefreshTableAsync(
        string schema, string table, CancellationToken ct = default)
    {
        var fresh = await _inspector.InspectTableAsync(schema, table, ct);

        // Atomic hot-swap inside the cache
        if (_cache is not null)
            _cache = new CacheEntry(
                ReplaceTable(_cache.Metadata, fresh), _cache.ExpiresAt);

        _logger.LogDebug("[MetadataService] Refreshed table {S}.{T}", schema, table);
        return fresh;
    }

    // ── FK fast-path ──────────────────────────────────────────────────────────

    /// <summary>Returns all FK relations — fast path for arrow-drawing on the canvas.</summary>
    public async Task<IReadOnlyList<ForeignKeyRelation>> GetForeignKeysAsync(
        CancellationToken ct = default) =>
        (await GetMetadataAsync(ct: ct)).AllForeignKeys;

    // ── Canvas table tracking ─────────────────────────────────────────────────

    /// <summary>Register a table that was placed on the canvas.</summary>
    public void AddCanvasTable(string fullTableName)
    {
        _canvasTables.Add(fullTableName);
        _logger.LogDebug("[Canvas] Added '{T}' — canvas size: {N}", fullTableName, _canvasTables.Count);
    }

    /// <summary>Remove a table that was deleted from the canvas.</summary>
    public void RemoveCanvasTable(string fullTableName)
    {
        _canvasTables.Remove(fullTableName);
        _logger.LogDebug("[Canvas] Removed '{T}' — canvas size: {N}", fullTableName, _canvasTables.Count);
    }

    /// <summary>Returns the tables currently on the canvas (snapshot, thread-safe).</summary>
    public IReadOnlyList<string> CanvasTables => _canvasTables.Snapshot();

    // ── Auto-Join (main canvas entry point) ───────────────────────────────────

    /// <summary>
    /// Called by the ViewModel immediately after <paramref name="newTable"/> is dropped.
    /// Uses the internally tracked canvas set.
    ///
    /// The canvas should render ghost JOIN nodes for high-confidence suggestions
    /// (score ≥ 0.85) immediately, and show lesser ones in a suggestion panel.
    /// </summary>
    public async Task<IReadOnlyList<JoinSuggestion>> SuggestJoinsAsync(
        string newTable, CancellationToken ct = default)
    {
        return await SuggestJoinsAsync(newTable, _canvasTables.Snapshot(), ct);
    }

    /// <summary>
    /// Stateless overload — caller supplies the canvas table set.
    /// Preferred in unit tests and ViewModels that own their own state.
    /// </summary>
    public async Task<IReadOnlyList<JoinSuggestion>> SuggestJoinsAsync(
        string newTable,
        IEnumerable<string> canvasTables,
        CancellationToken ct = default)
    {
        var metadata    = await GetMetadataAsync(ct: ct);
        var detector    = new AutoJoinDetector(metadata);
        var suggestions = detector.Suggest(newTable, canvasTables);

        _logger.LogInformation(
            "[AutoJoin] '{New}' → {N} suggestion(s): [{Pairs}]",
            newTable, suggestions.Count,
            string.Join(", ", suggestions.Select(s =>
                $"{s.ExistingTable}↔{s.NewTable}({s.Score:P0})")));

        return suggestions;
    }

    // ── Cache control ─────────────────────────────────────────────────────────

    public void InvalidateCache()
    {
        _cache = null;
        _logger.LogDebug("[MetadataService] Cache invalidated.");
    }

    // ── Rebuild helpers ───────────────────────────────────────────────────────

    private static DbMetadata ReplaceTable(DbMetadata original, TableMetadata fresh)
    {
        var newSchemas = original.Schemas
            .Select(s =>
            {
                if (!s.Name.Equals(fresh.Schema, StringComparison.OrdinalIgnoreCase))
                    return s;

                var newTables = s.Tables
                    .Select(t => t.FullName.Equals(fresh.FullName,
                        StringComparison.OrdinalIgnoreCase) ? fresh : t)
                    .ToList();

                return s with { Tables = newTables };
            })
            .ToList();

        return original with { Schemas = newSchemas };
    }
}

// ─── DI extension ─────────────────────────────────────────────────────────────

public static class MetadataServiceExtensions
{
    /// <summary>
    /// Registers the full metadata intelligence stack.
    /// Call after <c>services.AddVisualSqlArchitect()</c>.
    /// </summary>
    public static IServiceCollection AddMetadataIntelligence(
        this IServiceCollection services,
        TimeSpan? cacheTtl = null)
    {
        services.AddSingleton(sp =>
        {
            var ctx    = sp.GetRequiredService<ActiveConnectionContext>();
            var logger = sp.GetService<ILogger<MetadataService>>();
            // Config is available after SwitchAsync(); service is lazily initialised
            return MetadataService.Create(ctx.Config ?? throw new InvalidOperationException("No active connection configured"), cacheTtl, logger);
        });

        return services;
    }
}
