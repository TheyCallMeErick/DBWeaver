using Avalonia.Input;
using DBWeaver.UI.Services.Input.ShortcutRegistry;

namespace DBWeaver.Tests.Unit.Services.Input.ShortcutRegistry;

public sealed class ShortcutExecutionServiceTests
{
    [Fact]
    public void TryExecute_WithScopedContext_ResolvesCanvasShortcut()
    {
        IShortcutRegistry registry = new global::DBWeaver.UI.Services.Input.ShortcutRegistry.ShortcutRegistry(
            customizationStore: new NoOpShortcutCustomizationStore());
        var service = new ShortcutExecutionService(registry);
        string? resolvedAction = null;

        bool handled = service.TryExecute(
            new ShortcutExecutionContext(Key.F4, KeyModifiers.None, ShortcutContext.Canvas, AllowScopedShortcuts: true),
            definition =>
            {
                resolvedAction = definition.ActionId.Value;
                return true;
            });

        Assert.True(handled);
        Assert.Equal(ShortcutActionIds.ExplainPlan, resolvedAction);
    }

    [Fact]
    public void TryExecute_WhenScopedDisabled_OnlyResolvesGlobalShortcuts()
    {
        IShortcutRegistry registry = new global::DBWeaver.UI.Services.Input.ShortcutRegistry.ShortcutRegistry(
            customizationStore: new NoOpShortcutCustomizationStore());
        var service = new ShortcutExecutionService(registry);
        bool executed = false;

        bool handled = service.TryExecute(
            new ShortcutExecutionContext(Key.F4, KeyModifiers.None, ShortcutContext.Canvas, AllowScopedShortcuts: false),
            _ =>
            {
                executed = true;
                return true;
            });

        Assert.False(handled);
        Assert.False(executed);
    }
}
