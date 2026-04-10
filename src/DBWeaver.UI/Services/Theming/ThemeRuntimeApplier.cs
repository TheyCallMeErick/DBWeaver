using Avalonia;

namespace DBWeaver.UI.Services.Theming;

public static class ThemeRuntimeApplier
{
    public static int Apply(IDictionary<object, object?> resources, IReadOnlyDictionary<string, object> tokenOverrides)
    {
        int applied = 0;
        foreach ((string key, object value) in tokenOverrides)
        {
            resources[key] = value;
            applied++;
        }

        return applied;
    }

    public static int ApplyToCurrentApplication(IReadOnlyDictionary<string, object> tokenOverrides)
    {
        if (Application.Current is null)
            return 0;

        return Apply(Application.Current.Resources, tokenOverrides);
    }
}
