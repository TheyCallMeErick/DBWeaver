using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DBWeaver.UI.Services.Input.ShortcutRegistry;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.ViewModels.Shortcuts;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.Controls;

public sealed class KeyboardShortcutsWindow : Window
{
    private readonly KeyboardShortcutsViewModel _viewModel;

    public KeyboardShortcutsWindow(KeyboardShortcutsViewModel? viewModel = null)
    {
        _viewModel = viewModel ?? new KeyboardShortcutsViewModel(
            new global::DBWeaver.UI.Services.Input.ShortcutRegistry.ShortcutRegistry(
                customizationStore: new NoOpShortcutCustomizationStore()));

        Title = L("shortcuts.windowTitle", "Keyboard Shortcuts");
        Width = 980;
        Height = 760;
        MinWidth = 760;
        MinHeight = 560;
        Background = ResourceBrush("Bg0Brush", UiColorConstants.C_090B14);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        KeyDown += OnKeyDown;

        Content = new Border
        {
            Padding = new Thickness(16),
            Child = new KeyboardShortcutsEditorControl(_viewModel, showHeader: true),
        };
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        Close();
        e.Handled = true;
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private static IBrush ResourceBrush(string key, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out object? resource) == true && resource is IBrush brush)
            return brush;

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }
}
