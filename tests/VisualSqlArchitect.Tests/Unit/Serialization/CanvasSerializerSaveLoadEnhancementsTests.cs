using Avalonia;
using VisualSqlArchitect.UI.Serialization;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Serialization;

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
}
