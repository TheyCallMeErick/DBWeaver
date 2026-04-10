using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DBWeaver.UI.Controls;
using Avalonia.Styling;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.Services.Settings;
using DBWeaver.UI.Services.Theming;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI;

public partial class MainWindow
{
    private void SettingsBackdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        GetSettingsModule().CloseSettings();
    }

    private void SettingsDialog_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void SettingsCloseBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        GetSettingsModule().CloseSettings();
        e.Handled = true;
    }

    private void OnStartOpenSettingsRequested()
    {
        _globalModalManager.RequestSettings(keepStartVisible: true);
    }

    private void OpenSettings(bool keepStartVisible)
    {
        GetSettingsModule().OpenSettings(keepStartVisible);
        SyncLanguageComboSelection();
        EnsureKeyboardShortcutsSettingsPanel();
    }

    private void SettingsNavBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string sectionRaw)
        {
            e.Handled = true;
            return;
        }

        if (Enum.TryParse<ShellViewModel.ESettingsSection>(sectionRaw, ignoreCase: true, out var section))
            CurrentShell.SelectSettingsSection(section);

        e.Handled = true;
    }

    private void SettingsThemeDarkBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;

        AppSettingsStore.SaveThemeVariant("Dark");
        SetSettingsStatus(L("settings.status.darkApplied", "Tema escuro aplicado."), isError: false);

        e.Handled = true;
    }

    private void SettingsThemeLightBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = ThemeVariant.Light;

        AppSettingsStore.SaveThemeVariant("Light");
        SetSettingsStatus(L("settings.status.lightApplied", "Tema claro aplicado."), isError: false);

        e.Handled = true;
    }

    private void SettingsToggleSnapBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        EnsureCanvasInitialized();
        CurrentVm.ToggleSnapCommand.Execute(null);
        SetSettingsStatus(LF("settings.status.snapUpdated", "Snap atualizado: {0}.", CurrentVm.SnapToGridLabel), isError: false);
        e.Handled = true;
    }

    private void SettingsLanguageCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo || combo.SelectedItem is not ComboBoxItem item || item.Tag is not string culture)
            return;

        if (LocalizationService.Instance.SetCulture(culture))
        {
            _titleMenu = null;
            SetSettingsStatus(LF("settings.status.languageSelected", "Idioma selecionado: {0}.", culture), isError: false);
        }

        e.Handled = true;
    }

    private void SyncLanguageComboSelection()
    {
        ComboBox? combo = this.FindControl<ComboBox>("SettingsLanguageCombo");
        if (combo is null)
            return;

        foreach (object? option in combo.Items)
        {
            if (option is ComboBoxItem item && item.Tag is string culture &&
                string.Equals(culture, LocalizationService.Instance.CurrentCulture, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                break;
            }
        }
    }

    private void SettingsApplyThemeJsonBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        TextBox? jsonEditor = this.FindControl<TextBox>("SettingsThemeJsonTextBox");
        if (jsonEditor is null)
        {
            e.Handled = true;
            return;
        }

        string rawJson = jsonEditor.Text ?? string.Empty;
        ThemeJsonOperationResult result = _themeJsonSettings.ApplyAndPersist(rawJson);
        string warningSuffix = result.Warnings.Count > 0
            ? $" (avisos: {string.Join(" | ", result.Warnings)})"
            : string.Empty;
        SetSettingsStatus(result.Message + warningSuffix, isError: !result.Success);
        e.Handled = true;
    }

    private void SettingsRestoreDefaultThemeBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ThemeJsonOperationResult result = _themeJsonSettings.RestoreDefault();
        TextBox? jsonEditor = this.FindControl<TextBox>("SettingsThemeJsonTextBox");
        if (jsonEditor is not null)
            jsonEditor.Text = _themeJsonSettings.GetEditorJsonOrTemplate();

        SetSettingsStatus(result.Message, isError: !result.Success);
        e.Handled = true;
    }

    private void PopulateSettingsThemeJsonEditor()
    {
        TextBox? jsonEditor = this.FindControl<TextBox>("SettingsThemeJsonTextBox");
        if (jsonEditor is null)
            return;

        jsonEditor.Text = _themeJsonSettings.GetEditorJsonOrTemplate();
        SetSettingsStatus(L("settings.status.themeEditorReady", "Editor de tema pronto. Aplique para salvar e usar o tema."), isError: false);
    }

    private void EnsureKeyboardShortcutsSettingsPanel()
    {
        GetKeyboardShortcutsViewModel().Refresh();

        ContentControl? host = this.FindControl<ContentControl>("SettingsKeyboardShortcutsHost");
        if (host is null)
            return;

        if (host.Content is KeyboardShortcutsEditorControl)
            return;

        host.Content = new KeyboardShortcutsEditorControl(
            GetKeyboardShortcutsViewModel(),
            showHeader: false,
            statusCallback: (message, isError) => SetSettingsStatus(message, isError));
    }

    private void SetSettingsStatus(string message, bool isError)
    {
        TextBlock? statusText = this.FindControl<TextBlock>("SettingsThemeStatusText");
        if (statusText is null)
            return;

        statusText.Text = message;
        statusText.Foreground = isError
            ? ResourceBrush("StatusErrorBrush", UiColorConstants.C_F87171)
            : ResourceBrush("StatusOkBrush", UiColorConstants.C_34D399);
    }

    private SettingsWorkspaceModule GetSettingsModule()
    {
        _settingsModule ??= new SettingsWorkspaceModule(
            getShell: () => CurrentShell,
            enterCanvas: EnterCanvasMode
        );

        return _settingsModule;
    }
}
