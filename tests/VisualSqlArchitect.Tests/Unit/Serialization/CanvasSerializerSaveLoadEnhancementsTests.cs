using Avalonia;
using System.Text.Json;
using DBWeaver.Nodes;
using DBWeaver.UI.Serialization;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.Serialization;

public class CanvasSerializerSaveLoadEnhancementsTests
{
    [Fact]
    public async Task SaveToFileAsync_CompressesLargePayload_AndLoadStillWorks()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"vsaq_cmp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "canvas.vsaq");

        try
        {
            var vm = new CanvasViewModel();
            vm.InitializeDemoNodes();
            vm.Nodes[0].Parameters["blob"] = new string('x', CanvasSerializer.CompressionThresholdBytes * 2);

            await CanvasSerializer.SaveToFileAsync(path, vm, description: "large-payload");

            byte[] bytes = await File.ReadAllBytesAsync(path);
            Assert.True(bytes.Length > 2);
            Assert.Equal(0x1F, bytes[0]);
            Assert.Equal(0x8B, bytes[1]);
            Assert.True(CanvasSerializer.IsValidFile(path));

            var loadedVm = new CanvasViewModel();
            CanvasLoadResult result = await CanvasSerializer.LoadFromFileAsync(path, loadedVm);
            Assert.True(result.Success);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task SaveToFileAsync_OverwriteCreatesAutomaticBackup()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"vsaq_bak_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "canvas.vsaq");

        try
        {
            var vm = new CanvasViewModel();

            await CanvasSerializer.SaveToFileAsync(path, vm, description: "first-save");
            vm.Nodes.Add(new NodeViewModel("public.extra", [], new Point(400, 200)));
            await CanvasSerializer.SaveToFileAsync(path, vm, description: "second-save");

            string backupDir = Path.Combine(dir, ".vsaq_backups");
            Assert.True(Directory.Exists(backupDir));
            Assert.NotEmpty(Directory.EnumerateFiles(backupDir, "*.bak"));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LocalVersionHistory_CanRestoreOlderVersion()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"vsaq_ver_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "canvas.vsaq");

        try
        {
            var vm = new CanvasViewModel();
            await CanvasSerializer.SaveToFileAsync(path, vm, description: "v1");

            await Task.Delay(10);
            vm.Nodes.Add(new NodeViewModel("public.new_table", [], new Point(500, 260)));
            await CanvasSerializer.SaveToFileAsync(path, vm, description: "v2");

            IReadOnlyList<LocalFileVersionInfo> versions = CanvasSerializer.GetLocalFileVersions(path);
            Assert.True(versions.Count >= 2);

            LocalFileVersionInfo oldest = versions.OrderBy(v => v.CreatedAt).First();
            await CanvasSerializer.RestoreLocalVersionAsync(path, oldest.VersionPath);

            var meta = CanvasSerializer.ReadMeta(path);
            Assert.NotNull(meta);
            Assert.Equal("v1", meta?.Description);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SerializeDeserialize_TableSource_PreservesEffectivePinTypes()
    {
        var source = new CanvasViewModel();
        source.Nodes.Clear();
        source.Connections.Clear();

        source.Nodes.Add(new NodeViewModel(
            "dbo.Sancao",
            [
                ("id", PinDataType.Number),
                ("descricao", PinDataType.Text),
                ("ativo", PinDataType.Boolean)
            ],
            new Point(120, 80)
        ));

        string json = CanvasSerializer.Serialize(source);

        var loaded = new CanvasViewModel();
        loaded.Nodes.Clear();
        loaded.Connections.Clear();

        CanvasLoadResult result = CanvasSerializer.Deserialize(json, loaded);

        Assert.True(result.Success);
        NodeViewModel table = Assert.Single(loaded.Nodes);
        Assert.Equal(NodeType.TableSource, table.Type);

        Assert.Equal(PinDataType.Number, table.OutputPins.Single(p => p.Name == "id").EffectiveDataType);
        Assert.Equal(PinDataType.Text, table.OutputPins.Single(p => p.Name == "descricao").EffectiveDataType);
        Assert.Equal(PinDataType.Boolean, table.OutputPins.Single(p => p.Name == "ativo").EffectiveDataType);
    }

    [Fact]
    public void Deserialize_LegacyColumnRefColumns_UsesColumnLookupToRecoverPinTypes()
    {
        var source = new CanvasViewModel();
        source.Nodes.Clear();
        source.Connections.Clear();

        source.Nodes.Add(new NodeViewModel(
            "dbo.Sancao",
            [
                ("id", PinDataType.Number),
                ("descricao", PinDataType.Text)
            ],
            new Point(120, 80)
        ));

        string json = CanvasSerializer.Serialize(source);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        JsonElement node = root.GetProperty("Nodes")[0];

        string mutatedJson =
            $$"""
            {
              "Version": {{root.GetProperty("Version").GetInt32()}},
              "DatabaseProvider": "{{root.GetProperty("DatabaseProvider").GetString()}}",
              "ConnectionName": "{{root.GetProperty("ConnectionName").GetString()}}",
              "Zoom": {{root.GetProperty("Zoom").GetDouble()}},
              "PanX": {{root.GetProperty("PanX").GetDouble()}},
              "PanY": {{root.GetProperty("PanY").GetDouble()}},
              "Nodes": [
                {
                  "NodeId": "{{node.GetProperty("NodeId").GetString()}}",
                  "NodeType": "{{node.GetProperty("NodeType").GetString()}}",
                  "X": {{node.GetProperty("X").GetDouble()}},
                  "Y": {{node.GetProperty("Y").GetDouble()}},
                  "Alias": null,
                  "TableFullName": "dbo.Sancao",
                  "Parameters": {},
                  "PinLiterals": {},
                  "Columns": [
                    { "Name": "id", "Type": "ColumnRef" },
                    { "Name": "descricao", "Type": "ColumnRef" }
                  ]
                }
              ],
              "Connections": [],
              "SelectBindings": [],
              "WhereBindings": []
            }
            """;

        IReadOnlyDictionary<string, IReadOnlyList<(string Name, PinDataType Type)>> lookup =
            new Dictionary<string, IReadOnlyList<(string Name, PinDataType Type)>>(StringComparer.OrdinalIgnoreCase)
            {
                ["dbo.Sancao"] = [
                    ("id", PinDataType.Number),
                    ("descricao", PinDataType.Text)
                ]
            };

        var loaded = new CanvasViewModel();
        loaded.Nodes.Clear();
        loaded.Connections.Clear();

        CanvasLoadResult result = CanvasSerializer.Deserialize(mutatedJson, loaded, lookup);

        Assert.True(result.Success);
        NodeViewModel table = Assert.Single(loaded.Nodes);
        Assert.Equal(PinDataType.Number, table.OutputPins.Single(p => p.Name == "id").EffectiveDataType);
        Assert.Equal(PinDataType.Text, table.OutputPins.Single(p => p.Name == "descricao").EffectiveDataType);
    }
}
