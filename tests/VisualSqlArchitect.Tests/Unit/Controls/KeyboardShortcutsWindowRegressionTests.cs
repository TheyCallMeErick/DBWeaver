using DBWeaver.UI.Services.Input.ShortcutRegistry;

namespace DBWeaver.Tests.Unit.Controls;

public sealed class KeyboardShortcutsWindowRegressionTests
{
    [Fact]
    public void KeyboardShortcutsWindow_IncludesSqlEditorShortcutsSection()
    {
        var registry = new global::DBWeaver.UI.Services.Input.ShortcutRegistry.ShortcutRegistry(
            customizationStore: new NoOpShortcutCustomizationStore());

        Assert.NotNull(registry.FindByActionId(ShortcutActionIds.SqlTabNew));
        Assert.NotNull(registry.FindByActionId(ShortcutActionIds.SqlTabClose));
        Assert.NotNull(registry.FindByActionId(ShortcutActionIds.SqlTabOpenFile));
        Assert.NotNull(registry.FindByActionId(ShortcutActionIds.SqlTabSaveFile));
        Assert.NotNull(registry.FindByActionId(ShortcutActionIds.SqlRunAll));
        Assert.NotNull(registry.FindByActionId(ShortcutActionIds.SqlRunSelection));
        Assert.NotNull(registry.FindByActionId(ShortcutActionIds.SqlRunCurrent));
    }
}
