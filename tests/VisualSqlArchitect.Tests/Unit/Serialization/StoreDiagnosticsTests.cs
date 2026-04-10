using System.Reflection;
using System.Threading;
using DBWeaver.UI.Serialization;
using Xunit;

namespace DBWeaver.Tests.Unit.Serialization;

[Collection("StoreSerialization")]
public class StoreDiagnosticsTests
{
    private static void WriteAllTextWithRetry(string path, string content, int maxAttempts = 5, int delayMs = 25)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                File.WriteAllText(path, content);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(delayMs);
            }
        }

        File.WriteAllText(path, content);
    }

    private static void DeleteFileWithRetry(string path, int maxAttempts = 5, int delayMs = 25)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(delayMs);
            }
        }

        if (File.Exists(path))
            File.Delete(path);
    }

    private static void EnsureDirectoryAtPath(string path, int maxAttempts = 5, int delayMs = 25)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (File.Exists(path))
                    DeleteFileWithRetry(path, maxAttempts: 1, delayMs: delayMs);

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(delayMs);
            }
        }

        if (File.Exists(path))
            DeleteFileWithRetry(path);
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    [Fact]
    public void FlowVersionStore_LoadCorruptFile_RaisesWarning()
    {
        string path = (string)typeof(FlowVersionStore)
            .GetMethod("StorePath", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, null)!;

        bool existed = File.Exists(path);
        string? backup = existed ? File.ReadAllText(path) : null;
        var warnings = new List<string>();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            WriteAllTextWithRetry(path, "{ invalid json");

            FlowVersionStore.WarningRaised += warnings.Add;
            var loaded = FlowVersionStore.Load();

            Assert.Empty(loaded);
            Assert.Contains(
                warnings,
                w =>
                    !string.IsNullOrWhiteSpace(w)
                    && w.Contains(path, StringComparison.OrdinalIgnoreCase)
            );
        }
        finally
        {
            FlowVersionStore.WarningRaised -= warnings.Add;
            if (existed)
                WriteAllTextWithRetry(path, backup!);
            else if (File.Exists(path))
                DeleteFileWithRetry(path);
        }
    }

    [Fact]
    public void FlowVersionStore_SaveToDirectoryPath_RaisesWarning()
    {
        string path = (string)typeof(FlowVersionStore)
            .GetMethod("StorePath", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, null)!;

        bool existedFile = File.Exists(path);
        bool existedDir = Directory.Exists(path);
        string? backup = existedFile ? File.ReadAllText(path) : null;
        var warnings = new List<string>();

        try
        {
            EnsureDirectoryAtPath(path);

            FlowVersionStore.WarningRaised += warnings.Add;
            FlowVersionStore.Save([]);

            Assert.Contains(warnings, w => !string.IsNullOrWhiteSpace(w));
        }
        finally
        {
            FlowVersionStore.WarningRaised -= warnings.Add;
            if (Directory.Exists(path) && !existedDir)
                Directory.Delete(path);

            if (existedFile)
                WriteAllTextWithRetry(path, backup!);
        }
    }

    [Fact]
    public void SnippetStore_LoadCorruptFile_RaisesWarning()
    {
        string path = (string)typeof(SnippetStore)
            .GetMethod("StoreFilePath", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, null)!;

        bool existed = File.Exists(path);
        string? backup = existed ? File.ReadAllText(path) : null;
        var warnings = new List<string>();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            WriteAllTextWithRetry(path, "{ invalid json");

            SnippetStore.WarningRaised += warnings.Add;
            var loaded = SnippetStore.Load();

            Assert.Empty(loaded);
            Assert.Contains(
                warnings,
                w =>
                    !string.IsNullOrWhiteSpace(w)
                    && w.Contains(path, StringComparison.OrdinalIgnoreCase)
            );
        }
        finally
        {
            SnippetStore.WarningRaised -= warnings.Add;
            if (existed)
                WriteAllTextWithRetry(path, backup!);
            else if (File.Exists(path))
                DeleteFileWithRetry(path);
        }
    }

    [Fact]
    public void SnippetStore_SaveToDirectoryPath_RaisesWarning()
    {
        string path = (string)typeof(SnippetStore)
            .GetMethod("StoreFilePath", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, null)!;

        bool existedFile = File.Exists(path);
        bool existedDir = Directory.Exists(path);
        string? backup = existedFile ? File.ReadAllText(path) : null;
        var warnings = new List<string>();

        try
        {
            EnsureDirectoryAtPath(path);

            SnippetStore.WarningRaised += warnings.Add;
            SnippetStore.Save([]);

            Assert.Contains(warnings, w => !string.IsNullOrWhiteSpace(w));
        }
        finally
        {
            SnippetStore.WarningRaised -= warnings.Add;
            if (Directory.Exists(path) && !existedDir)
                Directory.Delete(path);

            if (existedFile)
                WriteAllTextWithRetry(path, backup!);
        }
    }
}
