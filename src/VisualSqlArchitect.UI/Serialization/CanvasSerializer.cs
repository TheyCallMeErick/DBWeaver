using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Serialization;

// ═════════════════════════════════════════════════════════════════════════════
// SERIALISATION DTOs  (independent of the runtime ViewModel types)
// ═════════════════════════════════════════════════════════════════════════════

public record SavedCanvas(
    int     Version,
    string  DatabaseProvider,
    string  ConnectionName,
    double  Zoom,
    double  PanX,
    double  PanY,
    List<SavedNode>       Nodes,
    List<SavedConnection> Connections,
    List<string>          SelectBindings,
    List<string>          WhereBindings
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
    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // ── Save ──────────────────────────────────────────────────────────────────

    public static string Serialize(CanvasViewModel vm,
        string provider = "Postgres", string connectionName = "untitled")
    {
        var saved = new SavedCanvas(
            Version:          2,
            DatabaseProvider: provider,
            ConnectionName:   connectionName,
            Zoom:             vm.Zoom,
            PanX:             vm.PanOffset.X,
            PanY:             vm.PanOffset.Y,
            Nodes:            vm.Nodes.Select(SerialiseNode).ToList(),
            Connections:      vm.Connections.Select(SerialiseConnection).ToList(),
            SelectBindings:   [],
            WhereBindings:    []
        );

        return JsonSerializer.Serialize(saved, _opts);
    }

    private static SavedNode SerialiseNode(NodeViewModel n) => new(
        NodeId:       n.Id,
        NodeType:     n.Type.ToString(),
        X:            n.Position.X,
        Y:            n.Position.Y,
        Alias:        n.Alias,
        TableFullName: n.Type == NodeType.TableSource ? n.Subtitle : null,
        Parameters:   new Dictionary<string, string>(n.Parameters),
        PinLiterals:  new Dictionary<string, string>(n.PinLiterals)
    );

    private static SavedConnection SerialiseConnection(ConnectionViewModel c) => new(
        FromNodeId:  c.FromPin.Owner.Id,
        FromPinName: c.FromPin.Name,
        ToNodeId:    c.ToPin?.Owner.Id ?? string.Empty,
        ToPinName:   c.ToPin?.Name    ?? string.Empty
    );

    // ── Load ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds a <see cref="CanvasViewModel"/> from JSON.
    /// Clears the existing canvas before loading.
    /// </summary>
    /// <exception cref="InvalidOperationException">On version mismatch or corrupt JSON.</exception>
    public static void Deserialize(string json, CanvasViewModel vm)
    {
        var saved = JsonSerializer.Deserialize<SavedCanvas>(json, _opts)
            ?? throw new InvalidOperationException("Failed to parse canvas JSON.");

        if (saved.Version < 1 || saved.Version > 2)
            throw new InvalidOperationException($"Unsupported canvas version: {saved.Version}");

        // Clear existing state
        vm.Connections.Clear();
        vm.Nodes.Clear();
        vm.UndoRedo.Clear();

        vm.Zoom      = saved.Zoom;
        vm.PanOffset = new Point(saved.PanX, saved.PanY);

        // Rebuild nodes
        var nodeMap = new Dictionary<string, NodeViewModel>(StringComparer.Ordinal);
        foreach (var sn in saved.Nodes)
        {
            var nodeVm = BuildNodeVm(sn);
            if (nodeVm is null) continue;
            nodeMap[sn.NodeId] = nodeVm;
            vm.Nodes.Add(nodeVm);
        }

        // Rebuild connections
        foreach (var sc in saved.Connections)
        {
            if (!nodeMap.TryGetValue(sc.FromNodeId, out var fromNode)) continue;
            if (!nodeMap.TryGetValue(sc.ToNodeId,   out var toNode))   continue;

            var fromPin = fromNode.OutputPins.FirstOrDefault(p => p.Name == sc.FromPinName)
                       ?? fromNode.InputPins.FirstOrDefault(p => p.Name == sc.FromPinName);
            var toPin   = toNode.InputPins.FirstOrDefault(p => p.Name == sc.ToPinName)
                       ?? toNode.OutputPins.FirstOrDefault(p => p.Name == sc.ToPinName);

            if (fromPin is null || toPin is null) continue;

            var conn = new ConnectionViewModel(fromPin, default, default)
            {
                ToPin = toPin
            };
            fromPin.IsConnected = true;
            toPin.IsConnected   = true;
            vm.Connections.Add(conn);
        }
    }

    private static NodeViewModel? BuildNodeVm(SavedNode sn)
    {
        if (!Enum.TryParse<NodeType>(sn.NodeType, out var nodeType))
            return null;

        NodeViewModel vm;

        if (nodeType == NodeType.TableSource && sn.TableFullName is not null)
        {
            // TableSource nodes need special reconstruction
            // (we don't have column metadata here so use minimal pins)
            vm = new NodeViewModel(sn.TableFullName,
                [], // columns re-populated on metadata refresh
                new Point(sn.X, sn.Y));
        }
        else
        {
            NodeDefinition def;
            try { def = NodeDefinitionRegistry.Get(nodeType); }
            catch { return null; }

            vm = new NodeViewModel(def, new Point(sn.X, sn.Y));
        }

        // Override ID to match saved ID (for connection mapping)
        // Since Id is init-only we use a workaround via reflection
        var idProp = typeof(NodeViewModel).GetProperty(nameof(NodeViewModel.Id));
        idProp?.SetValue(vm, sn.NodeId);

        vm.Alias = sn.Alias;

        foreach (var kv in sn.Parameters)
            vm.Parameters[kv.Key] = kv.Value;

        foreach (var kv in sn.PinLiterals)
            vm.PinLiterals[kv.Key] = kv.Value;

        return vm;
    }

    // ── File I/O helpers ──────────────────────────────────────────────────────

    public static async Task SaveToFileAsync(string path, CanvasViewModel vm,
        string provider = "Postgres", string connection = "untitled")
    {
        var json = Serialize(vm, provider, connection);
        await File.WriteAllTextAsync(path, json);
    }

    public static async Task LoadFromFileAsync(string path, CanvasViewModel vm)
    {
        var json = await File.ReadAllTextAsync(path);
        Deserialize(json, vm);
    }

    /// <summary>Returns true if the file looks like a valid saved canvas.</summary>
    public static bool IsValidFile(string path)
    {
        try
        {
            var json   = File.ReadAllText(path);
            var saved  = JsonSerializer.Deserialize<SavedCanvas>(json, _opts);
            return saved?.Version is >= 1 and <= 2;
        }
        catch { return false; }
    }
}
