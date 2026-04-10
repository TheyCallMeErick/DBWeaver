using DBWeaver.UI.Services.Input.ShortcutRegistry;
using DBWeaver.UI.Services.Settings;

namespace DBWeaver.Tests.Unit.Services.Input.ShortcutRegistrySubsystem;

public sealed class ShortcutCustomizationStoreTests
{
    [Fact]
    public void AppSettingsStore_TrySaveThenLoad_RoundTripsOverrides()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-shortcut-store-tests", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(root, "app.settings.json");
        AppSettingsStore.SettingsPathOverride = file;

        try
        {
            var store = new AppSettingsShortcutCustomizationStore();
            IReadOnlyList<ShortcutCustomizationEntry> overrides =
            [
                new(ShortcutActionIds.OpenCommandPalette, "Ctrl+Shift+K"),
                new(ShortcutActionIds.Save, "Ctrl+Alt+S"),
            ];

            bool persisted = store.TrySaveOverrides(overrides);
            IReadOnlyList<ShortcutCustomizationEntry> loaded = store.LoadOverrides();

            Assert.True(persisted);
            Assert.Equal(2, loaded.Count);
            Assert.Contains(loaded, item => item.ActionId == ShortcutActionIds.OpenCommandPalette && item.Gesture == "Ctrl+Shift+K");
            Assert.Contains(loaded, item => item.ActionId == ShortcutActionIds.Save && item.Gesture == "Ctrl+Alt+S");
        }
        finally
        {
            AppSettingsStore.SettingsPathOverride = null;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AppSettingsStore_LoadOverrides_IgnoresBlankActionIds()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-shortcut-store-tests", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(root, "app.settings.json");
        AppSettingsStore.SettingsPathOverride = file;

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(file, """
{
  "themeVariant": "Dark",
  "shortcuts": {
    "version": 1,
    "overrides": [
      { "actionId": "   ", "gesture": "Ctrl+1" },
      { "actionId": "shell.commandPalette.open", "gesture": "Ctrl+Shift+K" }
    ]
  }
}
""");

            var store = new AppSettingsShortcutCustomizationStore();
            IReadOnlyList<ShortcutCustomizationEntry> loaded = store.LoadOverrides();

            ShortcutCustomizationEntry item = Assert.Single(loaded);
            Assert.Equal(ShortcutActionIds.OpenCommandPalette, item.ActionId);
            Assert.Equal("Ctrl+Shift+K", item.Gesture);
        }
        finally
        {
            AppSettingsStore.SettingsPathOverride = null;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
