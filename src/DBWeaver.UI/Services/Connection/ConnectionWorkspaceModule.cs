using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services.Connection;

/// <summary>
/// Reusable coordinator for opening and activating connection workflows from different app entry points.
/// </summary>
public sealed class ConnectionWorkspaceModule
{
    private readonly Func<ConnectionManagerViewModel> _getConnectionManager;
    private readonly Action _activateConnectionSidebar;
    private readonly Action _enterCanvas;

    public ConnectionWorkspaceModule(
        Func<ConnectionManagerViewModel> getConnectionManager,
        Action activateConnectionSidebar,
        Action enterCanvas)
    {
        _getConnectionManager = getConnectionManager;
        _activateConnectionSidebar = activateConnectionSidebar;
        _enterCanvas = enterCanvas;
    }

    public void OpenManager(bool beginNewProfile, bool keepStartVisible)
    {
        ConnectionManagerViewModel manager = _getConnectionManager();

        _activateConnectionSidebar();
        manager.Open();

        if (beginNewProfile)
            manager.NewProfileCommand.Execute(null);

        if (!keepStartVisible)
            _enterCanvas();
    }

    public bool ConnectFromStartItem(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return false;

        ConnectionManagerViewModel manager = _getConnectionManager();
        ConnectionProfile? profile = manager.Profiles.FirstOrDefault(p =>
            string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase)
        );

        if (profile is null)
            return false;

        _activateConnectionSidebar();
        manager.Open();
        manager.SelectedProfile = profile;
        manager.ActiveProfileId = profile.Id;
        manager.ConnectCommand.Execute(null);
        manager.SelectedProfile = profile;
        _enterCanvas();
        return true;
    }
}
