using DBWeaver.UI.Services.Input.ShortcutRegistry;
using DBWeaver.UI.ViewModels.Shortcuts;

namespace DBWeaver.Tests.Unit.Controls;

public sealed class KeyboardShortcutsWindowIntegrationTests
{
    [Fact]
    public void SharedShortcutsViewModel_ExposesSqlEditorSectionForWindowAndSettings()
    {
        var registry = new global::DBWeaver.UI.Services.Input.ShortcutRegistry.ShortcutRegistry(
            customizationStore: new NoOpShortcutCustomizationStore());
        var viewModel = new KeyboardShortcutsViewModel(registry);

        Assert.Contains(viewModel.Sections, section =>
            section.Name.Contains("SQL", StringComparison.OrdinalIgnoreCase)
            && section.Items.Any(item => item.ActionId == ShortcutActionIds.SqlRunAll));
    }
}
