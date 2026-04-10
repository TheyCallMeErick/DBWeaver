using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services.Settings;

/// <summary>
/// Reusable coordinator for opening settings from different app entry points.
/// </summary>
public sealed class SettingsWorkspaceModule
{
    private readonly Func<ShellViewModel> _getShell;
    private readonly Action _enterCanvas;

    public SettingsWorkspaceModule(
        Func<ShellViewModel> getShell,
        Action enterCanvas)
    {
        _getShell = getShell;
        _enterCanvas = enterCanvas;
    }

    public void OpenSettings(bool keepStartVisible)
    {
        ShellViewModel shell = _getShell();

        if (!keepStartVisible)
            _enterCanvas();

        shell.OpenSettings();
    }

    public void CloseSettings()
    {
        _getShell().CloseSettings();
    }
}
