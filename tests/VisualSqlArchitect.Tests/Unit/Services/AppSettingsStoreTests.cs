using DBWeaver.UI.Services.Settings;
using DBWeaver.UI.Services.Input.ShortcutRegistry;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Services;

public class AppSettingsStoreTests
{
    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-settings-tests", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(root, "app.settings.json");
        AppSettingsStore.SettingsPathOverride = file;

        try
        {
            AppSettings settings = AppSettingsStore.Load();
            Assert.Equal("Dark", settings.ThemeVariant);
        }
        finally
        {
            AppSettingsStore.SettingsPathOverride = null;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsThemeVariant()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-settings-tests", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(root, "app.settings.json");
        AppSettingsStore.SettingsPathOverride = file;

        try
        {
            AppSettingsStore.Save(new AppSettings { ThemeVariant = "Light" });
            AppSettings loaded = AppSettingsStore.Load();

            Assert.Equal("Light", loaded.ThemeVariant);
        }
        finally
        {
            AppSettingsStore.SettingsPathOverride = null;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveThemeVariant_Blank_FallsBackToDark()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-settings-tests", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(root, "app.settings.json");
        AppSettingsStore.SettingsPathOverride = file;

        try
        {
            AppSettingsStore.SaveThemeVariant("   ");
            AppSettings loaded = AppSettingsStore.Load();

            Assert.Equal("Dark", loaded.ThemeVariant);
        }
        finally
        {
            AppSettingsStore.SettingsPathOverride = null;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Load_WithoutShortcutSection_UsesShortcutDefaults()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-settings-tests", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(root, "app.settings.json");
        AppSettingsStore.SettingsPathOverride = file;

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(file, """
{
  "themeVariant": "Light"
}
""");

            AppSettings settings = AppSettingsStore.Load();

            Assert.NotNull(settings.Shortcuts);
            Assert.Equal(1, settings.Shortcuts.Version);
            Assert.Empty(settings.Shortcuts.Overrides);
        }
        finally
        {
            AppSettingsStore.SettingsPathOverride = null;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsShortcutOverrides()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-settings-tests", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(root, "app.settings.json");
        AppSettingsStore.SettingsPathOverride = file;

        try
        {
            AppSettings settings = new()
            {
                ThemeVariant = "Dark",
                Shortcuts = new ShortcutSettingsSection
                {
                    Version = 1,
                    Overrides =
                    [
                        new("shell.commandPalette.open", "Ctrl+Shift+K"),
                        new("canvas.zoom.reset", "Ctrl+9"),
                    ],
                },
            };

            AppSettingsStore.Save(settings);
            AppSettings loaded = AppSettingsStore.Load();

            Assert.Equal(2, loaded.Shortcuts.Overrides.Count);
            Assert.Contains(loaded.Shortcuts.Overrides, item =>
                item.ActionId == "shell.commandPalette.open" && item.Gesture == "Ctrl+Shift+K");
            Assert.Contains(loaded.Shortcuts.Overrides, item =>
                item.ActionId == "canvas.zoom.reset" && item.Gesture == "Ctrl+9");
        }
        finally
        {
            AppSettingsStore.SettingsPathOverride = null;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveAndLoadSqlEditorExecutionHistory_PerProfile_RoundTripsAndCapsToFiveHundred()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-settings-tests", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(root, "app.settings.json");
        AppSettingsStore.SettingsPathOverride = file;

        try
        {
            List<SqlEditorHistoryEntry> entries = Enumerable.Range(1, 650)
                .Select(i => new SqlEditorHistoryEntry(
                    Sql: $"SELECT {i}",
                    Success: true,
                    RowsAffected: 1,
                    ExecutionTime: TimeSpan.FromMilliseconds(i),
                    ExecutedAt: DateTimeOffset.UtcNow.AddMinutes(-i)))
                .ToList();

            AppSettingsStore.SaveSqlEditorExecutionHistory("profile-a", entries);
            IReadOnlyList<SqlEditorHistoryEntry> loaded = AppSettingsStore.LoadSqlEditorExecutionHistory("profile-a");

            Assert.Equal(500, loaded.Count);
            Assert.True(loaded[0].ExecutedAt >= loaded[^1].ExecutedAt);
        }
        finally
        {
            AppSettingsStore.SettingsPathOverride = null;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ClearSqlEditorExecutionHistory_RemovesOnlyTargetProfile()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-settings-tests", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(root, "app.settings.json");
        AppSettingsStore.SettingsPathOverride = file;

        try
        {
            SqlEditorHistoryEntry entry = new("SELECT 1", true, 1, TimeSpan.FromMilliseconds(1), DateTimeOffset.UtcNow);
            AppSettingsStore.SaveSqlEditorExecutionHistory("profile-a", [entry]);
            AppSettingsStore.SaveSqlEditorExecutionHistory("profile-b", [entry with { Sql = "SELECT 2" }]);

            AppSettingsStore.ClearSqlEditorExecutionHistory("profile-a");

            Assert.Empty(AppSettingsStore.LoadSqlEditorExecutionHistory("profile-a"));
            Assert.Single(AppSettingsStore.LoadSqlEditorExecutionHistory("profile-b"));
        }
        finally
        {
            AppSettingsStore.SettingsPathOverride = null;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
