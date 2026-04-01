using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO.Compression;
using System.Text;
using Avalonia;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Serialization;

// ═════════════════════════════════════════════════════════════════════════════
// LOAD RESULT  (returned by Deserialize / LoadFromFileAsync)
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Describes the outcome of deserialising a canvas file.
/// <see cref="Warnings"/> is non-empty when the file was migrated from an older
/// schema version; the canvas is still valid and fully loaded in that case.
/// </summary>
public sealed record CanvasLoadResult(
    bool Success,
    string? Error = null,
    IReadOnlyList<string>? Warnings = null
)
{
    public static CanvasLoadResult Ok(IReadOnlyList<string>? warnings = null) =>
        new(true, null, warnings);

    public static CanvasLoadResult Fail(string error) => new(false, error, null);
}

// ═════════════════════════════════════════════════════════════════════════════
// SERIALISATION DTOs  (independent of the runtime ViewModel types)
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Top-level canvas save file.
///
/// Schema versions:
///   1 — initial release
///   2 — added alias, PinLiterals, OutputColumnOrder
///   3 — added AppVersion, CreatedAt, Description (this release)
/// </summary>
public record SavedCanvas(
    int Version,
    string DatabaseProvider,
    string ConnectionName,
    double Zoom,
    double PanX,
    double PanY,
    List<SavedNode> Nodes,
    List<SavedConnection> Connections,
    List<string> SelectBindings,
    List<string> WhereBindings,
    // ── v3 metadata fields (null in older files — filled during migration) ──
    string? AppVersion = null, // application version that wrote this file
    string? CreatedAt = null, // ISO-8601 UTC timestamp
    string? Description = null // optional user-supplied note
);

public record SavedColumn(
    string Name,
    string Type
);

public record SavedNode(
    string NodeId,
    string NodeType,
    double X,
    double Y,
    int? ZOrder,
    string? Alias,
    string? TableFullName,
    Dictionary<string, string> Parameters,
    Dictionary<string, string> PinLiterals,
    List<SavedColumn>? Columns = null,  // For TableSource: persisted column definitions
    SavedCteSubgraph? CteSubgraph = null
);

public record SavedCteSubgraph(
    List<SavedNode> Nodes,
    List<SavedConnection> Connections,
    string? ResultOutputNodeId
);

public record SavedConnection(
    string FromNodeId,
    string FromPinName,
    string ToNodeId,
    string ToPinName
);

public sealed record LocalFileVersionInfo(
    string VersionId,
    string VersionPath,
    DateTimeOffset CreatedAt,
    long SizeBytes,
    bool IsCompressed
)
{
    public string CreatedAtLocalLabel => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string SizeLabel => SizeBytes < 1024
        ? $"{SizeBytes} B"
        : SizeBytes < 1024 * 1024
            ? $"{SizeBytes / 1024.0:F1} KB"
            : $"{SizeBytes / (1024.0 * 1024.0):F1} MB";
}

// ═════════════════════════════════════════════════════════════════════════════
// SERIALISER
// ═════════════════════════════════════════════════════════════════════════════

public static class CanvasSerializer
{
    /// <summary>Current schema version written by this build.</summary>
    public const int CurrentSchemaVersion = 3;
    public const string CteSubgraphParameterKey = "__cteSubgraphJson";

    /// <summary>Semantic version of the application (bumped per release).</summary>
    public const string AppVersion = AppConstants.AppVersion;
    public const int CompressionThresholdBytes = 64 * 1024;
    public const int MaxLocalFileVersions = 30;
    public const int MaxAutomaticBackups = 20;

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Save ──────────────────────────────────────────────────────────────────

    public static string Serialize(
        CanvasViewModel vm,
        string provider = "Postgres",
        string connectionName = "untitled",
        string? description = null
    )
    {
        var saved = new SavedCanvas(
            Version: CurrentSchemaVersion,
            DatabaseProvider: provider,
            ConnectionName: connectionName,
            Zoom: vm.Zoom,
            PanX: vm.PanOffset.X,
            PanY: vm.PanOffset.Y,
            Nodes: [.. vm.Nodes.Select(n => SerialiseNode(n, vm.Nodes, vm.Connections))],
            Connections: [.. vm.Connections
                .Select(SerialiseConnection)
                .Where(c => c is not null)
                .Select(c => c!)],
            SelectBindings: [],
            WhereBindings: [],
            AppVersion: AppVersion,
            CreatedAt: DateTime.UtcNow.ToString("o"), // ISO-8601
            Description: description
        );

        return JsonSerializer.Serialize(saved, _opts);
    }

    // ── Subgraph helpers (used by snippet system) ─────────────────────────────

    /// <summary>
    /// Serialises a subset of nodes and the connections that are entirely
    /// internal to that subset (both endpoints in <paramref name="nodes"/>).
    /// </summary>
    public static (List<SavedNode> Nodes, List<SavedConnection> Connections) SerialiseSubgraph(
        IEnumerable<NodeViewModel> nodes,
        IEnumerable<ConnectionViewModel> connections
    )
    {
        List<NodeViewModel> nodeList = [.. nodes];
        HashSet<string> ids = nodeList.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        return (
            [.. nodeList.Select(n => SerialiseNode(n, nodeList, connections, includeCteSubgraph: false))],
            [.. connections.Where(c =>
                ids.Contains(c.FromPin.Owner.Id) && ids.Contains(c.ToPin?.Owner.Id ?? "")
            )
                .Select(SerialiseConnection)
                .Where(c => c is not null)
                .Select(c => c!)]
        );
    }

    /// <summary>
    /// Inserts a saved subgraph into the canvas, generating fresh node IDs and
    /// centering the pasted content at <paramref name="canvasPos"/>.
    /// </summary>
    public static void InsertSubgraph(
        List<SavedNode> nodes,
        List<SavedConnection> conns,
        CanvasViewModel vm,
        Point canvasPos,
        IReadOnlyDictionary<
            string,
            IReadOnlyList<(string Name, PinDataType Type)>
        >? columnLookup = null
    )
    {
        if (nodes.Count == 0)
            return;

        // Compute the centroid of the snippet's bounding box
        double cx = nodes.Average(n => n.X);
        double cy = nodes.Average(n => n.Y);

        // Map old node ID → new NodeViewModel with fresh ID + adjusted position
        var idMap = new Dictionary<string, NodeViewModel>(StringComparer.Ordinal);
        foreach (SavedNode sn in nodes)
        {
            SavedNode positioned = sn with
            {
                NodeId = Guid.NewGuid().ToString(),
                X = sn.X - cx + canvasPos.X,
                Y = sn.Y - cy + canvasPos.Y,
            };
            (NodeViewModel? nvm, _) = BuildNodeVm(positioned, columnLookup);
            if (nvm is null)
                continue;
            idMap[sn.NodeId] = nvm;
            vm.Nodes.Add(nvm);
        }

        // Rebuild connections using newly-assigned IDs
        foreach (SavedConnection sc in conns)
        {
            if (!idMap.TryGetValue(sc.FromNodeId, out NodeViewModel? fromNode))
                continue;
            if (!idMap.TryGetValue(sc.ToNodeId, out NodeViewModel? toNode))
                continue;

            PinViewModel? fromPin =
                fromNode.OutputPins.FirstOrDefault(p => p.Name == sc.FromPinName)
                ?? fromNode.InputPins.FirstOrDefault(p => p.Name == sc.FromPinName);
            PinViewModel? toPin =
                toNode.InputPins.FirstOrDefault(p => p.Name == sc.ToPinName)
                ?? toNode.OutputPins.FirstOrDefault(p => p.Name == sc.ToPinName);

            if (fromPin is null || toPin is null)
                continue;

            if (!IsConnectionCompatible(fromPin, toPin))
                continue;

            var conn = new ConnectionViewModel(fromPin, default, default) { ToPin = toPin };
            fromPin.IsConnected = true;
            toPin.IsConnected = true;
            vm.Connections.Add(conn);
        }
    }

    private static SavedNode SerialiseNode(
        NodeViewModel n,
        IEnumerable<NodeViewModel> allNodes,
        IEnumerable<ConnectionViewModel> allConnections,
        bool includeCteSubgraph = true
    )
    {
        var parameters = new Dictionary<string, string>(n.Parameters);
        parameters.Remove(CteSubgraphParameterKey);
        // Persist ResultOutput column order as a joined string
        if (n.Type == NodeType.ResultOutput && n.OutputColumnOrder.Count > 0)
            parameters["__colOrder"] = string.Join("|", n.OutputColumnOrder.Select(e => e.Key));

        // Persist TableSource columns
        List<SavedColumn>? columns = null;
        if (n.Type == NodeType.TableSource)
        {
            columns = n.OutputPins
                .Select(p => new SavedColumn(p.Name, p.EffectiveDataType.ToString()))
                .ToList();
        }

        SavedCteSubgraph? cteSubgraph = includeCteSubgraph
            ? BuildCteSubgraph(n, allNodes, allConnections)
            : null;

        return new(
            NodeId: n.Id,
            NodeType: n.Type.ToString(),
            X: n.Position.X,
            Y: n.Position.Y,
            ZOrder: n.ZOrder,
            Alias: n.Alias,
            TableFullName: n.Type == NodeType.TableSource ? n.Subtitle : null,
            Parameters: parameters,
            PinLiterals: new Dictionary<string, string>(n.PinLiterals),
            Columns: columns,
            CteSubgraph: cteSubgraph
        );
    }

    private static SavedCteSubgraph? BuildCteSubgraph(
        NodeViewModel node,
        IEnumerable<NodeViewModel> allNodes,
        IEnumerable<ConnectionViewModel> allConnections
    )
    {
        if (node.Type != NodeType.CteDefinition)
            return null;

        if (node.Parameters.TryGetValue(CteSubgraphParameterKey, out string? payload)
            && !string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                SavedCteSubgraph? persisted = JsonSerializer.Deserialize<SavedCteSubgraph>(payload);
                if (persisted is not null)
                    return persisted;
            }
            catch
            {
                // Fall back to graph extraction when payload is malformed.
            }
        }

        ConnectionViewModel? queryWire = allConnections.FirstOrDefault(c =>
            c.ToPin?.Owner == node
            && c.ToPin.Name == "query"
            && c.FromPin.Owner.Type == NodeType.ResultOutput
        );
        if (queryWire?.FromPin.Owner is not NodeViewModel resultOutput)
            return null;

        HashSet<string> upstream = CollectUpstreamNodeIds(resultOutput, allConnections, includeCteDefinitions: false);

        var scopedNodes = allNodes.Where(n => upstream.Contains(n.Id)).ToList();
        var scopedIds = scopedNodes.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var scopedConnections = allConnections.Where(c =>
            c.ToPin is not null
            && scopedIds.Contains(c.FromPin.Owner.Id)
            && scopedIds.Contains(c.ToPin!.Owner.Id)
        );

        return new SavedCteSubgraph(
            Nodes: [.. scopedNodes.Select(n => SerialiseNode(n, scopedNodes, scopedConnections, includeCteSubgraph: false))],
            Connections: [.. scopedConnections
                .Select(SerialiseConnection)
                .Where(c => c is not null)
                .Select(c => c!)],
            ResultOutputNodeId: resultOutput.Id
        );
    }

    private static HashSet<string> CollectUpstreamNodeIds(
        NodeViewModel sinkNode,
        IEnumerable<ConnectionViewModel> allConnections,
        bool includeCteDefinitions
    )
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { sinkNode.Id };
        var queue = new Queue<string>();
        queue.Enqueue(sinkNode.Id);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (ConnectionViewModel conn in allConnections.Where(c => c.ToPin?.Owner.Id == current))
            {
                NodeViewModel fromOwner = conn.FromPin.Owner;
                if (!includeCteDefinitions && fromOwner.Type == NodeType.CteDefinition)
                    continue;

                if (visited.Add(fromOwner.Id))
                    queue.Enqueue(fromOwner.Id);
            }
        }

        return visited;
    }

    private static SavedConnection? SerialiseConnection(ConnectionViewModel c)
    {
        if (c.ToPin is null)
            return null;

        return new SavedConnection(
            FromNodeId: c.FromPin.Owner.Id,
            FromPinName: c.FromPin.Name,
            ToNodeId: c.ToPin.Owner.Id,
            ToPinName: c.ToPin.Name
        );
    }

    // ── Migration pipeline ────────────────────────────────────────────────────

    /// <summary>
    /// Upgrades a <see cref="SavedCanvas"/> from any supported older version to
    /// <see cref="CurrentSchemaVersion"/>.  Returns the migrated canvas and a list
    /// of human-readable migration notes (empty when no migration was needed).
    /// </summary>
    private static (SavedCanvas Canvas, List<string> Warnings) MigrateToLatest(SavedCanvas canvas)
    {
        var warnings = new List<string>();
        SavedCanvas c = canvas;
        int originalVersion = c.Version;

        // v1 → v2: no structural changes; alias/PinLiterals defaulted to empty
        if (c.Version == 1)
        {
            warnings.Add("Migration step applied: schema v1 -> v2 (compatibility defaults).");
            c = c with { Version = 2 };
        }

        // v2 → v3: add metadata fields
        if (c.Version == 2)
        {
            warnings.Add(
                "Migration step applied: schema v2 -> v3 (metadata backfill for AppVersion/CreatedAt)."
            );
            c = c with
            {
                Version = CurrentSchemaVersion,
                AppVersion = "unknown (pre-v3)",
                CreatedAt = DateTime.UtcNow.ToString("o"),
            };
        }

        // Some early v3 files may still miss metadata fields; backfill them assistively.
        if (
            c.Version == CurrentSchemaVersion
            && (string.IsNullOrWhiteSpace(c.AppVersion) || string.IsNullOrWhiteSpace(c.CreatedAt))
        )
        {
            warnings.Add(
                "Migration step applied: schema v3 metadata normalization (filled missing AppVersion/CreatedAt)."
            );
            c = c with
            {
                AppVersion = string.IsNullOrWhiteSpace(c.AppVersion)
                    ? "unknown (pre-v3)"
                    : c.AppVersion,
                CreatedAt = string.IsNullOrWhiteSpace(c.CreatedAt)
                    ? DateTime.UtcNow.ToString("o")
                    : c.CreatedAt,
            };
        }

        if (originalVersion != c.Version)
            warnings.Add($"Migration summary: schema v{originalVersion} -> v{c.Version}.");

        return (c, warnings);
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds a <see cref="CanvasViewModel"/> from JSON.
    /// Clears the existing canvas before loading.
    /// Returns a <see cref="CanvasLoadResult"/> — check <see cref="CanvasLoadResult.Success"/>
    /// before using the canvas; inspect <see cref="CanvasLoadResult.Warnings"/> for migration notes.
    /// </summary>
    /// <param name="columnLookup">Optional catalog to restore TableSource column pins.</param>
    public static CanvasLoadResult Deserialize(
        string json,
        CanvasViewModel vm,
        IReadOnlyDictionary<string, IReadOnlyList<(string Name, PinDataType Type)>>? columnLookup =
            null
    )
    {
        SavedCanvas? raw;
        try
        {
            raw = JsonSerializer.Deserialize<SavedCanvas>(json, _opts);
        }
        catch (JsonException ex)
        {
            return CanvasLoadResult.Fail($"Invalid JSON: {ex.Message}");
        }

        if (raw is null)
            return CanvasLoadResult.Fail("Canvas file is empty or could not be parsed.");

        if (raw.Version < 1 || raw.Version > CurrentSchemaVersion)
            return CanvasLoadResult.Fail(
                $"Unsupported schema version {raw.Version}. "
                    + $"This build supports versions 1–{CurrentSchemaVersion}."
            );

        // Apply forward migrations
        (SavedCanvas saved, List<string> warnings) = MigrateToLatest(raw);

        // Clear existing state
        vm.Connections.Clear();
        vm.Nodes.Clear();
        vm.UndoRedo.Clear();

        vm.Zoom = saved.Zoom;
        vm.PanOffset = new Point(saved.PanX, saved.PanY);

        // Rebuild nodes
        var nodeMap = new Dictionary<string, NodeViewModel>(StringComparer.Ordinal);
        var skippedNodes = new List<(string NodeId, string NodeType, string Reason)>();

        foreach (SavedNode sn in saved.Nodes)
        {
            (NodeViewModel? nodeVm, string? skipReason) = BuildNodeVm(sn, columnLookup);
            if (nodeVm is null)
            {
                skippedNodes.Add((sn.NodeId, sn.NodeType, skipReason ?? "Unknown error"));
                continue;
            }
            nodeMap[sn.NodeId] = nodeVm;
            vm.Nodes.Add(nodeVm);
        }

        // Normalize Z-order after load (supports old files without ZOrder and prevents duplicates/gaps).
        var loadOrderById = saved.Nodes
            .Select((n, i) => (n.NodeId, i))
            .ToDictionary(x => x.NodeId, x => x.i, StringComparer.Ordinal);

        int z = 0;
        foreach (NodeViewModel n in vm.Nodes
                     .OrderBy(n => n.ZOrder)
                     .ThenBy(n => loadOrderById.TryGetValue(n.Id, out int idx) ? idx : int.MaxValue))
            n.ZOrder = z++;

        // Log warning if nodes were skipped
        if (skippedNodes.Count > 0)
        {
            var skippedSummary = string.Join(", ",
                skippedNodes.GroupBy(s => s.NodeType)
                    .Select(g => $"{g.Count()} {g.Key}"));
            warnings.Add(
                $"Warning: {skippedNodes.Count} node(s) could not be loaded and were skipped: {skippedSummary}. " +
                "This may indicate the file was created with a newer version or has unsupported node types."
            );
        }

        // Rebuild connections
        int malformedConnections = 0;
        int unresolvedConnections = 0;
        int incompatibleConnections = 0;
        int migratedLegacyProjectionPins = 0;

        foreach (SavedConnection sc in saved.Connections)
        {
            if (string.IsNullOrWhiteSpace(sc.FromNodeId)
                || string.IsNullOrWhiteSpace(sc.ToNodeId)
                || string.IsNullOrWhiteSpace(sc.FromPinName)
                || string.IsNullOrWhiteSpace(sc.ToPinName))
            {
                malformedConnections++;
                continue;
            }

            if (!nodeMap.TryGetValue(sc.FromNodeId, out NodeViewModel? fromNode))
            {
                unresolvedConnections++;
                continue;
            }
            if (!nodeMap.TryGetValue(sc.ToNodeId, out NodeViewModel? toNode))
            {
                unresolvedConnections++;
                continue;
            }

            PinViewModel? fromPin =
                fromNode.OutputPins.FirstOrDefault(p => p.Name == sc.FromPinName)
                ?? fromNode.InputPins.FirstOrDefault(p => p.Name == sc.FromPinName);
            PinViewModel? toPin =
                toNode.InputPins.FirstOrDefault(p => p.Name == sc.ToPinName)
                ?? toNode.OutputPins.FirstOrDefault(p => p.Name == sc.ToPinName);

            // ColumnList/ColumnSetBuilder: redirect old dynamic pins (col_N)
            // to the canonical fixed "columns" pin.
            if (
                toPin is null
                && (toNode.IsColumnList || toNode.Type == NodeType.ColumnSetBuilder)
                && sc.ToPinName.StartsWith("col_", StringComparison.OrdinalIgnoreCase)
            )
            {
                toPin = toNode.InputPins.FirstOrDefault(p => p.Name == "columns");
                if (toPin is not null)
                    migratedLegacyProjectionPins++;
            }

            // AND/OR dynamic pins: create cond_N on-the-fly if not yet present.
            if (toPin is null && toNode.IsLogicGate && sc.ToPinName.StartsWith("cond_"))
            {
                var dynPin = new PinViewModel(
                    new PinDescriptor(
                        sc.ToPinName,
                        PinDirection.Input,
                        PinDataType.Boolean,
                        IsRequired: false,
                        Description: "Connect a boolean condition"
                    ),
                    toNode
                );
                toNode.InputPins.Add(dynPin);
                toPin = dynPin;
            }

            // WindowFunction dynamic pins: create partition_N/order_N on-the-fly if missing.
            if (
                toPin is null
                && toNode.IsWindowFunction
                && (
                    sc.ToPinName.StartsWith("partition_", StringComparison.OrdinalIgnoreCase)
                    || sc.ToPinName.StartsWith("order_", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                var dynPin = new PinViewModel(
                    new PinDescriptor(
                        sc.ToPinName,
                        PinDirection.Input,
                        PinDataType.ColumnRef,
                        IsRequired: false,
                        Description: "Connect a column or expression"
                    ),
                    toNode
                );
                toNode.InputPins.Add(dynPin);
                toPin = dynPin;
            }

            if (fromPin is null || toPin is null)
            {
                unresolvedConnections++;
                continue;
            }

            if (!IsConnectionCompatible(fromPin, toPin))
            {
                incompatibleConnections++;
                continue;
            }

            var conn = new ConnectionViewModel(fromPin, default, default) { ToPin = toPin };
            fromPin.IsConnected = true;
            toPin.IsConnected = true;
            vm.Connections.Add(conn);
        }

        if (malformedConnections > 0)
            warnings.Add(
                $"Skipped {malformedConnections} malformed connection(s) with missing endpoint data."
            );

        if (unresolvedConnections > 0)
            warnings.Add(
                $"Skipped {unresolvedConnections} connection(s) that reference missing nodes or pins."
            );

        if (incompatibleConnections > 0)
            warnings.Add(
                $"Skipped {incompatibleConnections} incompatible connection(s) due to type mismatch."
            );

        if (migratedLegacyProjectionPins > 0)
            warnings.Add(
                $"Migrated {migratedLegacyProjectionPins} legacy projection connection(s) from dynamic 'col_*' pins to canonical 'columns' pin."
            );

        // Restore ResultOutput column order after all connections exist
        foreach (SavedNode sn in saved.Nodes)
        {
            if (!nodeMap.TryGetValue(sn.NodeId, out NodeViewModel? nodeVm))
                continue;
            if (nodeVm.Type != NodeType.ResultOutput)
                continue;

            // First sync normally (builds entries from connections)
            nodeVm.SyncOutputColumns(vm.Connections);

            // Then apply saved order if present
            if (!sn.Parameters.TryGetValue("__colOrder", out string? colOrderStr))
                continue;
            string[] savedOrder = colOrderStr.Split('|', StringSplitOptions.RemoveEmptyEntries);
            ApplySavedColumnOrder(nodeVm, savedOrder);
        }

        return CanvasLoadResult.Ok(warnings.Count > 0 ? warnings : null);
    }

    private static void MaterializeCteSubgraphs(
        SavedCanvas saved,
        CanvasViewModel vm,
        Dictionary<string, NodeViewModel> nodeMap,
        List<string> warnings,
        IReadOnlyDictionary<string, IReadOnlyList<(string Name, PinDataType Type)>>? columnLookup
    )
    {
        foreach (SavedNode cteNode in saved.Nodes.Where(n => n.CteSubgraph is not null))
        {
            if (!nodeMap.TryGetValue(cteNode.NodeId, out NodeViewModel? cteVm))
                continue;

            if (cteVm.Type != NodeType.CteDefinition)
                continue;

            bool hasQueryWire = vm.Connections.Any(c =>
                c.ToPin?.Owner == cteVm
                && c.ToPin.Name == "query"
                && c.FromPin.Owner.Type == NodeType.ResultOutput
            );
            if (hasQueryWire)
                continue;

            SavedCteSubgraph subgraph = cteNode.CteSubgraph!;
            if (subgraph.Nodes.Count == 0)
                continue;

            var localIdMap = new Dictionary<string, NodeViewModel>(StringComparer.Ordinal);
            foreach (SavedNode subNode in subgraph.Nodes)
            {
                (NodeViewModel? subVm, string? skipReason) = BuildNodeVm(subNode, columnLookup);
                if (subVm is null)
                {
                    warnings.Add($"Skipped CTE subgraph node '{subNode.NodeType}' for '{cteNode.NodeId}': {skipReason ?? "Unknown error"}.");
                    continue;
                }

                while (nodeMap.ContainsKey(subVm.Id))
                    subVm.Id = Guid.NewGuid().ToString("N")[..8];

                nodeMap[subVm.Id] = subVm;
                localIdMap[subNode.NodeId] = subVm;
                vm.Nodes.Add(subVm);
            }

            foreach (SavedConnection sc in subgraph.Connections)
            {
                if (!localIdMap.TryGetValue(sc.FromNodeId, out NodeViewModel? fromNode))
                    continue;
                if (!localIdMap.TryGetValue(sc.ToNodeId, out NodeViewModel? toNode))
                    continue;

                PinViewModel? fromPin =
                    fromNode.OutputPins.FirstOrDefault(p => p.Name == sc.FromPinName)
                    ?? fromNode.InputPins.FirstOrDefault(p => p.Name == sc.FromPinName);
                PinViewModel? toPin =
                    toNode.InputPins.FirstOrDefault(p => p.Name == sc.ToPinName)
                    ?? toNode.OutputPins.FirstOrDefault(p => p.Name == sc.ToPinName);

                if (fromPin is null || toPin is null)
                    continue;

                if (!IsConnectionCompatible(fromPin, toPin))
                    continue;

                var conn = new ConnectionViewModel(fromPin, default, default) { ToPin = toPin };
                fromPin.IsConnected = true;
                toPin.IsConnected = true;
                vm.Connections.Add(conn);
            }

            NodeViewModel? resultVm = null;
            if (!string.IsNullOrWhiteSpace(subgraph.ResultOutputNodeId))
                localIdMap.TryGetValue(subgraph.ResultOutputNodeId, out resultVm);

            resultVm ??= localIdMap.Values.FirstOrDefault(n => n.Type == NodeType.ResultOutput);
            if (resultVm is null)
                continue;

            PinViewModel? resultPin = resultVm.OutputPins.FirstOrDefault(p => p.Name == "result");
            PinViewModel? queryPin = cteVm.InputPins.FirstOrDefault(p => p.Name == "query");
            if (resultPin is null || queryPin is null)
                continue;

            if (!IsConnectionCompatible(resultPin, queryPin))
                continue;

            var queryConn = new ConnectionViewModel(resultPin, default, default) { ToPin = queryPin };
            resultPin.IsConnected = true;
            queryPin.IsConnected = true;
            vm.Connections.Add(queryConn);

            warnings.Add($"Materialized persisted CTE subgraph for node '{cteVm.Id}'.");
        }
    }

    private static (NodeViewModel?, string? SkipReason) BuildNodeVm(
        SavedNode sn,
        IReadOnlyDictionary<string, IReadOnlyList<(string Name, PinDataType Type)>>? columnLookup
    )
    {
        if (!Enum.TryParse<NodeType>(sn.NodeType, out NodeType nodeType))
            return (null, $"Unknown node type '{sn.NodeType}' (not supported in this version)");

        NodeViewModel vm;

        if (nodeType == NodeType.TableSource && sn.TableFullName is not null)
        {
            // Restore columns — prefer persisted columns, fall back to lookup catalog
            IEnumerable<(string, PinDataType)> cols = [];

            IReadOnlyDictionary<string, PinDataType>? lookupColumnTypes = null;
            if (
                columnLookup is not null
                && columnLookup.TryGetValue(
                    sn.TableFullName,
                    out IReadOnlyList<(string Name, PinDataType Type)>? foundLookup
                )
            )
            {
                lookupColumnTypes = foundLookup.ToDictionary(
                    c => c.Name,
                    c => c.Type,
                    StringComparer.OrdinalIgnoreCase
                );
            }

            // First: use persisted columns if available
            if (sn.Columns is { Count: > 0 })
            {
                cols = sn.Columns
                    .Select(c => (
                        c.Name,
                        ResolveSavedColumnType(c, lookupColumnTypes)
                    ))
                    .ToList();
            }
            // Fallback: use lookup catalog
            else if (
                columnLookup is not null
                && columnLookup.TryGetValue(
                    sn.TableFullName,
                    out IReadOnlyList<(string Name, PinDataType Type)>? found
                )
            )
                cols = found;

            vm = new NodeViewModel(sn.TableFullName, cols, new Point(sn.X, sn.Y));
        }
        else
        {
            NodeDefinition def;
            try
            {
                def = NodeDefinitionRegistry.Get(nodeType);
            }
            catch (Exception ex)
            {
                return (null, $"NodeDefinition not found for type '{nodeType}': {ex.Message}");
            }

            vm = new NodeViewModel(def, new Point(sn.X, sn.Y));
        }

        // Override ID to match saved ID (for connection mapping)
        // Since Id is init-only we use a workaround via reflection
        System.Reflection.PropertyInfo? idProp = typeof(NodeViewModel).GetProperty(
            nameof(NodeViewModel.Id)
        );
        if (idProp is null || !idProp.CanWrite)
            return (null, "Could not restore node ID (Id property is not writable).");

        try
        {
            idProp.SetValue(vm, sn.NodeId);
        }
        catch (Exception ex)
        {
            return (null, $"Could not restore node ID '{sn.NodeId}': {ex.Message}");
        }

        // Old files may not contain layer info; defer normalization in Deserialize.
        vm.ZOrder = sn.ZOrder ?? 0;

        vm.Alias = sn.Alias;

        foreach (KeyValuePair<string, string> kv in sn.Parameters)
            vm.Parameters[kv.Key] = kv.Value;

        foreach (KeyValuePair<string, string> kv in sn.PinLiterals)
            vm.PinLiterals[kv.Key] = kv.Value;

        if (sn.CteSubgraph is not null)
            vm.Parameters[CteSubgraphParameterKey] = JsonSerializer.Serialize(sn.CteSubgraph);

        return (vm, null);
    }

    private static PinDataType ResolveSavedColumnType(
        SavedColumn column,
        IReadOnlyDictionary<string, PinDataType>? lookupColumnTypes
    )
    {
        if (Enum.TryParse<PinDataType>(column.Type, out PinDataType parsedType))
        {
            if (
                parsedType != PinDataType.ColumnRef
                || lookupColumnTypes is null
                || !lookupColumnTypes.TryGetValue(column.Name, out PinDataType resolved)
            )
            {
                return parsedType;
            }

            return resolved;
        }

        if (
            lookupColumnTypes is not null
            && lookupColumnTypes.TryGetValue(column.Name, out PinDataType fromLookup)
        )
        {
            return fromLookup;
        }

        return PinDataType.ColumnRef;
    }

    private static void ApplySavedColumnOrder(NodeViewModel nodeVm, IReadOnlyList<string> savedOrder)
    {
        for (int i = 0; i < savedOrder.Count; i++)
        {
            if (i >= nodeVm.OutputColumnOrder.Count)
                break;

            string key = savedOrder[i];
            int currentIndex = -1;
            for (int idx = 0; idx < nodeVm.OutputColumnOrder.Count; idx++)
            {
                if (nodeVm.OutputColumnOrder[idx].Key != key)
                    continue;

                currentIndex = idx;
                break;
            }

            if (currentIndex < 0 || currentIndex == i)
                continue;

            nodeVm.OutputColumnOrder.Move(currentIndex, i);
        }
    }

    // ── File I/O helpers ──────────────────────────────────────────────────────

    public static async Task SaveToFileAsync(
        string path,
        CanvasViewModel vm,
        string provider = "Postgres",
        string connection = "untitled",
        string? description = null
    )
    {
        string json = Serialize(vm, provider, connection, description);
        byte[] utf8 = Encoding.UTF8.GetBytes(json);
        bool useCompression = utf8.Length >= CompressionThresholdBytes;
        byte[] payload = useCompression ? CompressBytes(utf8) : utf8;

        string? parentDir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parentDir))
            Directory.CreateDirectory(parentDir);

        await CreateAutomaticBackupAsync(path);

        await File.WriteAllBytesAsync(path, payload);
        await AddLocalFileVersionAsync(path, payload);
    }

    /// <summary>
    /// Loads a canvas file and returns a <see cref="CanvasLoadResult"/>.
    /// Check <see cref="CanvasLoadResult.Success"/> before using the canvas.
    /// <see cref="CanvasLoadResult.Warnings"/> is non-empty when the file was
    /// migrated from an older schema version.
    /// </summary>
    public static async Task<CanvasLoadResult> LoadFromFileAsync(
        string path,
        CanvasViewModel vm,
        IReadOnlyDictionary<string, IReadOnlyList<(string Name, PinDataType Type)>>? columnLookup =
            null
    )
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(path);
        }
        catch (Exception ex)
        {
            return CanvasLoadResult.Fail($"Could not read file: {ex.Message}");
        }

        string json;
        try
        {
            json = DecodeCanvasJson(bytes);
        }
        catch (Exception ex)
        {
            return CanvasLoadResult.Fail($"Could not decode canvas file: {ex.Message}");
        }

        return Deserialize(json, vm, columnLookup);
    }

    /// <summary>
    /// Returns true if the file is a readable canvas file with a supported schema version.
    /// </summary>
    public static bool IsValidFile(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            string json = DecodeCanvasJson(bytes);
            SavedCanvas? saved = JsonSerializer.Deserialize<SavedCanvas>(json, _opts);
            return saved?.Version is >= 1 and <= CurrentSchemaVersion;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reads just the metadata fields from a file without fully loading the canvas.
    /// Returns null if the file cannot be parsed.
    /// </summary>
    public static (
        int Version,
        string? AppVersion,
        string? CreatedAt,
        string? Description
    )? ReadMeta(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            string json = DecodeCanvasJson(bytes);
            SavedCanvas? saved = JsonSerializer.Deserialize<SavedCanvas>(json, _opts);
            if (saved is null)
                return null;
            return (saved.Version, saved.AppVersion, saved.CreatedAt, saved.Description);
        }
        catch
        {
            return null;
        }
    }

    public static IReadOnlyList<LocalFileVersionInfo> GetLocalFileVersions(string targetFilePath)
    {
        string historyDir = GetHistoryDirectory(targetFilePath);
        if (!Directory.Exists(historyDir))
            return [];

        return Directory
            .EnumerateFiles(historyDir, "*.vsaq*")
            .Select(path =>
            {
                var fi = new FileInfo(path);
                DateTimeOffset createdAt = fi.LastWriteTimeUtc;
                string fileName = Path.GetFileNameWithoutExtension(path);
                int sep = fileName.IndexOf('_');
                if (sep > 0
                    && DateTimeOffset.TryParseExact(
                        fileName[..sep],
                        "yyyyMMddHHmmssfff",
                        null,
                        System.Globalization.DateTimeStyles.AssumeUniversal,
                        out DateTimeOffset parsed
                    ))
                {
                    createdAt = parsed;
                }

                return new LocalFileVersionInfo(
                    VersionId: fileName,
                    VersionPath: path,
                    CreatedAt: createdAt,
                    SizeBytes: fi.Length,
                    IsCompressed: path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                );
            })
            .OrderByDescending(v => v.CreatedAt)
            .ToList();
    }

    public static async Task RestoreLocalVersionAsync(string targetFilePath, string versionFilePath)
    {
        await CreateAutomaticBackupAsync(targetFilePath);
        byte[] bytes = await File.ReadAllBytesAsync(versionFilePath);

        string? parentDir = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrWhiteSpace(parentDir))
            Directory.CreateDirectory(parentDir);

        await File.WriteAllBytesAsync(targetFilePath, bytes);
    }

    private static bool IsConnectionCompatible(PinViewModel fromPin, PinViewModel toPin) =>
        toPin.CanAccept(fromPin);

    private static bool IsGZipPayload(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B;

    private static string DecodeCanvasJson(byte[] bytes)
    {
        if (!IsGZipPayload(bytes))
            return Encoding.UTF8.GetString(bytes);

        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static byte[] CompressBytes(byte[] utf8)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            gzip.Write(utf8, 0, utf8.Length);
        return output.ToArray();
    }

    private static async Task CreateAutomaticBackupAsync(string targetFilePath)
    {
        if (!File.Exists(targetFilePath))
            return;

        string backupDir = GetBackupDirectory(targetFilePath);
        Directory.CreateDirectory(backupDir);

        string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        string backupPath = Path.Combine(
            backupDir,
            $"{stamp}_{Path.GetFileName(targetFilePath)}.bak"
        );

        await using (FileStream src = File.OpenRead(targetFilePath))
        await using (FileStream dst = File.Create(backupPath))
            await src.CopyToAsync(dst);

        PruneOldFiles(backupDir, MaxAutomaticBackups);
    }

    private static async Task AddLocalFileVersionAsync(string targetFilePath, byte[] payload)
    {
        string historyDir = GetHistoryDirectory(targetFilePath);
        Directory.CreateDirectory(historyDir);

        string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        bool compressed = IsGZipPayload(payload);
        string ext = compressed ? ".vsaq.gz" : ".vsaq";
        string versionPath = Path.Combine(historyDir, $"{stamp}_{Path.GetFileNameWithoutExtension(targetFilePath)}{ext}");

        await File.WriteAllBytesAsync(versionPath, payload);
        PruneOldFiles(historyDir, MaxLocalFileVersions);
    }

    private static void PruneOldFiles(string dir, int keep)
    {
        FileInfo[] files = new DirectoryInfo(dir)
            .EnumerateFiles()
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .ToArray();

        foreach (FileInfo stale in files.Skip(keep))
        {
            try
            {
                stale.Delete();
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    private static string GetHistoryDirectory(string targetFilePath)
    {
        string parent = Path.GetDirectoryName(targetFilePath) ?? ".";
        string baseName = Path.GetFileNameWithoutExtension(targetFilePath);
        return Path.Combine(parent, ".vsaq_history", baseName);
    }

    private static string GetBackupDirectory(string targetFilePath)
    {
        string parent = Path.GetDirectoryName(targetFilePath) ?? ".";
        return Path.Combine(parent, ".vsaq_backups");
    }
}
