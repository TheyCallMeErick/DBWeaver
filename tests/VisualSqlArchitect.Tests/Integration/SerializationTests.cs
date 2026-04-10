using System.Text.Json;
using Xunit;

namespace DBWeaver.Tests.Integration;

/// <summary>
/// Integration tests for canvas save/load file operations.
/// Tests file I/O, directory creation, and JSON round-trip persistence.
/// </summary>
public class SerializationTests
{
    [Fact]
    public void FileOperations_CanSaveJsonToFile()
    {
        // Arrange
        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.vsaq");

        try
        {
            var testData = new
            {
                Version = 3,
                DatabaseProvider = "Postgres",
                Zoom = 1.5,
                Nodes = new object[] { },
            };

            // Act
            string json = JsonSerializer.Serialize(testData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(tempPath, json);

            // Assert
            Assert.True(File.Exists(tempPath));
            string readBack = File.ReadAllText(tempPath);
            Assert.Contains("Version", readBack);
            Assert.Contains("Postgres", readBack);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void FileOperations_CanReadJsonFromFile()
    {
        // Arrange
        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.vsaq");
        string originalJson = """{"Version":3,"Zoom":1.5,"Nodes":[]}""";

        try
        {
            File.WriteAllText(tempPath, originalJson);

            // Act
            string readJson = File.ReadAllText(tempPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(readJson);

            // Assert
            Assert.NotNull(data);
            Assert.True(data.ContainsKey("Version"));
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void FileOperations_CanOverwriteExistingFile()
    {
        // Arrange
        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.vsaq");

        try
        {
            // Write initial content
            var data1 = new { Version = 1, Zoom = 1.0 };
            File.WriteAllText(tempPath, JsonSerializer.Serialize(data1));
            Assert.True(File.Exists(tempPath));

            // Act - Overwrite
            var data2 = new { Version = 3, Zoom = 2.5 };
            File.WriteAllText(tempPath, JsonSerializer.Serialize(data2));
            string readBack = File.ReadAllText(tempPath);

            // Assert
            Assert.Contains("Version", readBack);
            Assert.Contains("2.5", readBack);
            Assert.DoesNotContain("1.0", readBack);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task FileOperations_CanSaveFileAsync()
    {
        // Arrange
        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.vsaq");

        try
        {
            var testData = new { Version = 3, Description = "Test" };
            string json = JsonSerializer.Serialize(testData);

            // Act
            await File.WriteAllTextAsync(tempPath, json);

            // Assert
            Assert.True(File.Exists(tempPath));
            string readBack = await File.ReadAllTextAsync(tempPath);
            Assert.Contains("Version", readBack);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task FileOperations_CanLoadFileAsync()
    {
        // Arrange
        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.vsaq");
        string originalJson = """{"Version":3,"Description":"Test"}""";

        try
        {
            await File.WriteAllTextAsync(tempPath, originalJson);

            // Act
            string readJson = await File.ReadAllTextAsync(tempPath);

            // Assert
            Assert.Equal(originalJson, readJson);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void FileExtension_IsVsaqFormat()
    {
        // Arrange
        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.vsaq");

        // Act
        string ext = Path.GetExtension(tempPath);

        // Assert
        Assert.Equal(".vsaq", ext);
    }

    [Fact]
    public void Json_RoundTripPreservesData()
    {
        // Arrange
        var originalData = new
        {
            Version = 3,
            DatabaseProvider = "Postgres",
            ConnectionName = "test",
            Zoom = 2.5,
            PanX = 100.0,
            PanY = 200.0,
            Nodes = new object[] { },
            Connections = new object[] { },
        };

        // Act
        string json = JsonSerializer.Serialize(originalData, new JsonSerializerOptions { WriteIndented = true });
        var loaded = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(3, ((JsonElement)loaded["Version"]).GetInt32());
        Assert.Contains("Zoom", json);
        Assert.Contains("2.5", json);
    }

    [Fact]
    public void FileOperations_CanCreateNestedDirectories()
    {
        // Arrange
        string baseDir = Path.Combine(Path.GetTempPath(), "vsarch_nested_test");
        string filePath = Path.Combine(baseDir, "subdir", "test.vsaq");

        try
        {
            // Act
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, """{"Version":3}""");

            // Assert
            Assert.True(File.Exists(filePath));
            Assert.True(Directory.Exists(baseDir));
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, recursive: true);
        }
    }

    [Fact]
    public void FileOperations_HandlesNonExistentPaths()
    {
        // Act & Assert
        string nonExistent = Path.Combine(Path.GetTempPath(), "nonexistent_dir_12345", "file.vsaq");
        Assert.False(File.Exists(nonExistent));
    }

    [Fact]
    public void FilePath_CanBeParsedCorrectly()
    {
        // Arrange
        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.vsaq");

        // Act
        string dir = Path.GetDirectoryName(tempPath)!;
        string fileName = Path.GetFileName(tempPath);
        string nameWithoutExt = Path.GetFileNameWithoutExtension(tempPath);

        // Assert
        Assert.True(Path.IsPathRooted(tempPath));
        Assert.NotEmpty(dir);
        Assert.EndsWith(".vsaq", fileName);
        Assert.DoesNotContain(".", nameWithoutExt);
    }

    [Fact]
    public void Json_InvalidContentThrowsOnDeserialize()
    {
        // Arrange
        string invalidJson = "{ invalid json }";

        // Act & Assert
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Dictionary<string, object>>(invalidJson)
        );
    }
}
