using DBWeaver.UI.Services.SqlEditor;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlEditorFileServiceTests
{
    [Fact]
    public async Task SaveAsync_WithMissingPath_ReturnsCanceledOutcome()
    {
        var sut = new SqlEditorFileService();

        SqlEditorFileSaveOutcome outcome = await sut.SaveAsync("SELECT 1;", null);

        Assert.False(outcome.Success);
        Assert.False(outcome.HasError);
        AssertLocalized(outcome.StatusText, "Salvamento cancelado.", "Save canceled.");
    }

    [Fact]
    public async Task SaveAsync_WithValidPath_WritesFile()
    {
        var sut = new SqlEditorFileService();
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sql");
        try
        {
            SqlEditorFileSaveOutcome outcome = await sut.SaveAsync("SELECT 1;", path);

            Assert.True(outcome.Success);
            Assert.False(outcome.HasError);
            Assert.True(File.Exists(path));
            Assert.Equal("SELECT 1;", await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task OpenAsync_MissingFile_ReturnsFailure()
    {
        var sut = new SqlEditorFileService();

        SqlEditorFileOpenOutcome outcome = await sut.OpenAsync("/tmp/non-existing-vsa-file.sql");

        Assert.False(outcome.Success);
        Assert.True(outcome.HasError);
        Assert.Null(outcome.Content);
        AssertLocalized(outcome.StatusText, "Falha ao abrir arquivo SQL.", "Open failed.");
    }

    [Fact]
    public async Task OpenAsync_ExistingFile_ReturnsContent()
    {
        var sut = new SqlEditorFileService();
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sql");
        await File.WriteAllTextAsync(path, "SELECT 7;");
        try
        {
            SqlEditorFileOpenOutcome outcome = await sut.OpenAsync(path);

            Assert.True(outcome.Success);
            Assert.False(outcome.HasError);
            Assert.Equal("SELECT 7;", outcome.Content);
            Assert.Equal(path, outcome.DetailText);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static void AssertLocalized(string? actual, params string[] expectedValues)
    {
        Assert.NotNull(actual);
        Assert.Contains(actual!, expectedValues);
    }
}
