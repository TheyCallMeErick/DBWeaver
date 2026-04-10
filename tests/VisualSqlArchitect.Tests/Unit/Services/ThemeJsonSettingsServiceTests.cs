using DBWeaver.UI.Services.Settings;

namespace DBWeaver.Tests.Unit.Services;

public class ThemeJsonSettingsServiceTests
{
    [Fact]
    public void GetEditorJsonOrTemplate_NoFile_ReturnsTemplate()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-theme-json-tests", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(root, "user-theme.json");
        var sut = new ThemeJsonSettingsService(() => file);

        try
        {
            string text = sut.GetEditorJsonOrTemplate();
            Assert.Contains("colors", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ApplyAndPersist_ValidJson_SavesFile()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-theme-json-tests", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(root, "user-theme.json");
        var sut = new ThemeJsonSettingsService(() => file);

        const string json = """
        {
          "colors": {
                        "bg0": "#0B1020",
            "textPrimary": "#E8EAED",
            "textSecondary": "#8B95A8"
          }
        }
        """;

        try
        {
            ThemeJsonOperationResult result = sut.ApplyAndPersist(json);

            Assert.True(result.Success);
            Assert.True(File.Exists(file));
            Assert.False(string.IsNullOrWhiteSpace(result.Message));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ApplyAndPersist_InvalidJson_ReturnsError()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-theme-json-tests", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(root, "user-theme.json");
        var sut = new ThemeJsonSettingsService(() => file);

        try
        {
            ThemeJsonOperationResult result = sut.ApplyAndPersist("{ invalid-json");
            Assert.False(result.Success);
            Assert.True(
                result.Message.Contains("inval", StringComparison.OrdinalIgnoreCase)
                || result.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase)
            );
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RestoreDefault_DeletesFile()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-theme-json-tests", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(root, "user-theme.json");
        Directory.CreateDirectory(root);
        File.WriteAllText(file, "{}");

        var sut = new ThemeJsonSettingsService(() => file);

        try
        {
            ThemeJsonOperationResult result = sut.RestoreDefault();

            Assert.True(result.Success);
            Assert.False(File.Exists(file));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
