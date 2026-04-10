using DBWeaver.UI.Services.Connection;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.Services;

public class ConnectionWorkspaceModuleTests
{
    [Fact]
    public void OpenManager_FromStart_KeepsStartVisible_AndCanBeginNewProfile()
    {
        var manager = new ConnectionManagerViewModel();
        var enteredCanvas = false;
        var sidebarActivated = false;

        var module = new ConnectionWorkspaceModule(
            getConnectionManager: () => manager,
            activateConnectionSidebar: () => sidebarActivated = true,
            enterCanvas: () => enteredCanvas = true
        );

        module.OpenManager(beginNewProfile: true, keepStartVisible: true);

        Assert.True(sidebarActivated);
        Assert.True(manager.IsVisible);
        Assert.True(manager.IsEditing);
        Assert.False(enteredCanvas);
    }

    [Fact]
    public void OpenManager_FromEditor_EntersCanvas()
    {
        var manager = new ConnectionManagerViewModel();
        var enteredCanvas = false;

        var module = new ConnectionWorkspaceModule(
            getConnectionManager: () => manager,
            activateConnectionSidebar: () => { },
            enterCanvas: () => enteredCanvas = true
        );

        module.OpenManager(beginNewProfile: false, keepStartVisible: false);

        Assert.True(manager.IsVisible);
        Assert.True(enteredCanvas);
    }

    [Fact]
    public void ConnectFromStartItem_SelectsProfile_AndEntersCanvas()
    {
        var manager = new ConnectionManagerViewModel();
        manager.Profiles.Clear();

        var profile = new ConnectionProfile
        {
            Id = "profile-1",
            Name = "Local PG",
            Provider = DBWeaver.Core.DatabaseProvider.Postgres,
            Host = "localhost",
            Port = 5432,
            Database = "demo",
            Username = "demo",
            Password = "demo",
            TimeoutSeconds = 1,
        };
        manager.Profiles.Add(profile);

        var enteredCanvas = false;
        var module = new ConnectionWorkspaceModule(
            getConnectionManager: () => manager,
            activateConnectionSidebar: () => { },
            enterCanvas: () => enteredCanvas = true
        );

        bool connected = module.ConnectFromStartItem("profile-1");

        Assert.True(connected);
        Assert.True(enteredCanvas);
        Assert.Equal("profile-1", manager.ActiveProfileId);
        Assert.Same(profile, manager.SelectedProfile);
    }
}
