namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Resolves key gestures against the shortcut registry and dispatches execution callbacks.
/// </summary>
public sealed class ShortcutExecutionService
{
    private readonly IShortcutRegistry _registry;

    public ShortcutExecutionService(IShortcutRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public bool TryExecute(
        ShortcutExecutionContext context,
        Func<ShortcutDefinition, bool> executeDefinition)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(executeDefinition);

        ShortcutContext lookupContext = context.AllowScopedShortcuts
            ? context.PreferredContext
            : ShortcutContext.Global;
        ShortcutDefinition? definition = _registry.FindByGesture(context.Key, context.Modifiers, lookupContext);
        if (definition is null)
            return false;

        return executeDefinition(definition);
    }
}
