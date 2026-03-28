using System.Text.Json;
using System.Text.Json.Serialization;
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

public record SavedNode(
    string NodeId,
    string NodeType,
    double X,
    double Y,
    string? Alias,
    string? TableFullName,
    Dictionary<string, string> Parameters,
    Dictionary<string, string> PinLiterals
);

public record SavedConnection(
    string FromNodeId,
    string FromPinName,
    string ToNodeId,
    string ToPinName
);

// ═════════════════════════════════════════════════════════════════════════════
// SERIALISER
// ═════════════════════════════════════════════════════════════════════════════

public static class CanvasSerializer
{
    /// <summary>Current schema version written by this build.</summary>
    public const int CurrentSchemaVersion = 3;

    /// <summary>Semantic version of the application (bumped per release).</summary>
    public const string AppVersion = "1.0.0";

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
            Nodes: [.. vm.Nodes.Select(SerialiseNode)],
            Connections: [.. vm.Connections.Select(SerialiseConnection)],
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
            [.. nodeList.Select(SerialiseNode)],
            [.. connections.Where(c =>
                ids.Contains(c.FromPin.Owner.Id) && ids.Contains(c.ToPin?.Owner.Id ?? "")
            ).Select(SerialiseConnection)]
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
            NodeViewModel? nvm = BuildNodeVm(positioned, columnLookup);
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

            var conn = new ConnectionViewModel(fromPin, default, default) { ToPin = toPin };
            fromPin.IsConnected = true;
            toPin.IsConnected = true;
            vm.Connections.Add(conn);
        }
    }

    private static SavedNode SerialiseNode(NodeViewModel n)
    {
        var parameters = new Dictionary<string, string>(n.Parameters);
        // Persist ResultOutput column order as a joined string
        if (n.Type == NodeType.ResultOutput && n.OutputColumnOrder.Count > 0)
            parameters["__colOrder"] = string.Join("|", n.OutputColumnOrder.Select(e => e.Key));

        return new(
            NodeId: n.Id,
            NodeType: n.Type.ToString(),
            X: n.Position.X,
            Y: n.Position.Y,
            Alias: n.Alias,
            TableFullName: n.Type == NodeType.TableSource ? n.Subtitle : null,
            Parameters: parameters,
            PinLiterals: new Dictionary<string, string>(n.PinLiterals)
        );
    }

    private static SavedConnection SerialiseConnection(ConnectionViewModel c) =>
        new(
            FromNodeId: c.FromPin.Owner.Id,
            FromPinName: c.FromPin.Name,
            ToNodeId: c.ToPin?.Owner.Id ?? string.Empty,
            ToPinName: c.ToPin?.Name ?? string.Empty
        );

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

        // v1 → v2: no structural changes; alias/PinLiterals defaulted to empty
        if (c.Version == 1)
        {
            warnings.Add("File was created with schema v1 — migrated to v3 (no data loss).");
            c = c with { Version = 2 };
        }

        // v2 → v3: add metadata fields
        if (c.Version == 2)
        {
            warnings.Add(
                $"File was created with schema v2 — migrated to v3. "
                    + $"AppVersion set to 'unknown (pre-v3)', CreatedAt set to import time."
            );
            c = c with
            {
                Version = CurrentSchemaVersion,
                AppVersion = "unknown (pre-v3)",
                CreatedAt = DateTime.UtcNow.ToString("o"),
            };
        }

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
        foreach (SavedNode sn in saved.Nodes)
        {
            NodeViewModel? nodeVm = BuildNodeVm(sn, columnLookup);
            if (nodeVm is null)
                continue;
            nodeMap[sn.NodeId] = nodeVm;
            vm.Nodes.Add(nodeVm);
        }

        // Rebuild connections
        foreach (SavedConnection sc in saved.Connections)
        {
            if (!nodeMap.TryGetValue(sc.FromNodeId, out NodeViewModel? fromNode))
                continue;
            if (!nodeMap.TryGetValue(sc.ToNodeId, out NodeViewModel? toNode))
                continue;

            PinViewModel? fromPin =
                fromNode.OutputPins.FirstOrDefault(p => p.Name == sc.FromPinName)
                ?? fromNode.InputPins.FirstOrDefault(p => p.Name == sc.FromPinName);
            PinViewModel? toPin =
                toNode.InputPins.FirstOrDefault(p => p.Name == sc.ToPinName)
                ?? toNode.OutputPins.FirstOrDefault(p => p.Name == sc.ToPinName);

            // ColumnList: redirect old dynamic pins (col_N) to the new fixed "columns" pin
            if (toPin is null && toNode.IsColumnList && sc.ToPinName.StartsWith("col_"))
            {
                toPin = toNode.InputPins.FirstOrDefault(p => p.Name == "columns");
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

            if (fromPin is null || toPin is null)
                continue;

            var conn = new ConnectionViewModel(fromPin, default, default) { ToPin = toPin };
            fromPin.IsConnected = true;
            toPin.IsConnected = true;
            vm.Connections.Add(conn);
        }

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
            for (int i = 0; i < savedOrder.Length; i++)
            {
                string key = savedOrder[i];
                (OutputColumnEntry e, int idx) cur = nodeVm
                    .OutputColumnOrder.Select((e, idx) => (e, idx))
                    .FirstOrDefault(x => x.e.Key == key);
                if (cur.e is null)
                    continue;
                if (cur.idx != i && i < nodeVm.OutputColumnOrder.Count)
                    nodeVm.OutputColumnOrder.Move(cur.idx, i);
            }
        }

        return CanvasLoadResult.Ok(warnings.Count > 0 ? warnings : null);
    }

    private static NodeViewModel? BuildNodeVm(
        SavedNode sn,
        IReadOnlyDictionary<string, IReadOnlyList<(string Name, PinDataType Type)>>? columnLookup
    )
    {
        if (!Enum.TryParse<NodeType>(sn.NodeType, out NodeType nodeType))
            return null;

        NodeViewModel vm;

        if (nodeType == NodeType.TableSource && sn.TableFullName is not null)
        {
            // Restore columns from lookup catalog when available
            IEnumerable<(string, PinDataType)> cols = [];
            if (
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
            catch
            {
                return null;
            }

            vm = new NodeViewModel(def, new Point(sn.X, sn.Y));
        }

        // Override ID to match saved ID (for connection mapping)
        // Since Id is init-only we use a workaround via reflection
        System.Reflection.PropertyInfo? idProp = typeof(NodeViewModel).GetProperty(
            nameof(NodeViewModel.Id)
        );
        idProp?.SetValue(vm, sn.NodeId);

        vm.Alias = sn.Alias;

        foreach (KeyValuePair<string, string> kv in sn.Parameters)
            vm.Parameters[kv.Key] = kv.Value;

        foreach (KeyValuePair<string, string> kv in sn.PinLiterals)
            vm.PinLiterals[kv.Key] = kv.Value;

        return vm;
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
        await File.WriteAllTextAsync(path, json);
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
        string json;
        try
        {
            json = await File.ReadAllTextAsync(path);
        }
        catch (Exception ex)
        {
            return CanvasLoadResult.Fail($"Could not read file: {ex.Message}");
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
            string json = File.ReadAllText(path);
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
            string json = File.ReadAllText(path);
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
}
