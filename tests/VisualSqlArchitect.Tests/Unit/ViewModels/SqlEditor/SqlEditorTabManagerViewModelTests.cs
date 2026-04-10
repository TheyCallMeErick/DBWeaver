using DBWeaver.Core;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.SqlEditor;

public sealed class SqlEditorTabManagerViewModelTests
{
    [Fact]
    public void Initialize_CreatesSingleScriptTabWithProvidedProvider()
    {
        var sut = new SqlEditorTabManagerViewModel();

        sut.Initialize(DatabaseProvider.SQLite, "profile-1");

        Assert.Single(sut.Tabs);
        Assert.Equal(0, sut.ActiveTabIndex);
        Assert.Equal("Script 1", sut.Tabs[0].FallbackTitle);
        Assert.Equal(DatabaseProvider.SQLite, sut.Tabs[0].Provider);
        Assert.Equal("profile-1", sut.Tabs[0].ConnectionProfileId);
    }

    [Fact]
    public void AddNewTab_UsesActiveContextAndActivatesCreatedTab()
    {
        var sut = new SqlEditorTabManagerViewModel();
        sut.Initialize(DatabaseProvider.MySql, "profile-a");

        SqlEditorTabState created = sut.AddNewTab();

        Assert.Equal(2, sut.Tabs.Count);
        Assert.Equal(1, sut.ActiveTabIndex);
        Assert.Same(created, sut.Tabs[1]);
        Assert.Equal(DatabaseProvider.MySql, created.Provider);
        Assert.Equal("profile-a", created.ConnectionProfileId);
        Assert.Equal("Script 2", created.FallbackTitle);
    }

    [Fact]
    public void TryActivate_InvalidOrCurrentIndex_ReturnsFalse()
    {
        var sut = new SqlEditorTabManagerViewModel();
        sut.Initialize(DatabaseProvider.Postgres);
        sut.AddNewTab();

        Assert.False(sut.TryActivate(-1));
        Assert.False(sut.TryActivate(5));
        Assert.False(sut.TryActivate(sut.ActiveTabIndex));
    }

    [Fact]
    public void TryActivate_ValidIndex_SwitchesActiveTab()
    {
        var sut = new SqlEditorTabManagerViewModel();
        sut.Initialize(DatabaseProvider.Postgres);
        sut.AddNewTab();

        bool changed = sut.TryActivate(0);

        Assert.True(changed);
        Assert.Equal(0, sut.ActiveTabIndex);
    }

    [Fact]
    public void CloseTab_WithSingleTab_DoesNotRemoveAnything()
    {
        var sut = new SqlEditorTabManagerViewModel();
        sut.Initialize(DatabaseProvider.Postgres);

        int active = sut.CloseTab(0);

        Assert.Single(sut.Tabs);
        Assert.Equal(0, active);
        Assert.Equal(0, sut.ActiveTabIndex);
    }

    [Fact]
    public void CloseTab_WhenRemovingTabBeforeActive_ReindexesActiveTab()
    {
        var sut = new SqlEditorTabManagerViewModel();
        sut.Initialize(DatabaseProvider.Postgres);
        sut.AddNewTab();
        sut.AddNewTab();
        Assert.Equal(2, sut.ActiveTabIndex);

        int newActive = sut.CloseTab(0);

        Assert.Equal(1, newActive);
        Assert.Equal(1, sut.ActiveTabIndex);
        Assert.Equal(2, sut.Tabs.Count);
    }

    [Fact]
    public void ReceiveFromCanvas_UsesActiveTabWhenItIsEmpty()
    {
        var sut = new SqlEditorTabManagerViewModel();
        sut.Initialize(DatabaseProvider.Postgres);

        sut.ReceiveFromCanvas("SELECT 1;", DatabaseProvider.SqlServer);

        Assert.Single(sut.Tabs);
        Assert.Equal("SELECT 1;", sut.Tabs[0].SqlText);
        Assert.Equal(DatabaseProvider.SqlServer, sut.Tabs[0].Provider);
        Assert.False(sut.Tabs[0].IsDirty);
    }

    [Fact]
    public void ReceiveFromCanvas_CreatesNewTabWhenActiveHasText()
    {
        var sut = new SqlEditorTabManagerViewModel();
        sut.Initialize(DatabaseProvider.Postgres, "profile-7");
        sut.Tabs[0].SqlText = "SELECT old;";
        sut.Tabs[0].IsDirty = true;

        sut.ReceiveFromCanvas("SELECT fresh;", DatabaseProvider.SQLite);

        Assert.Equal(2, sut.Tabs.Count);
        Assert.Equal(1, sut.ActiveTabIndex);
        Assert.Equal("SELECT fresh;", sut.Tabs[1].SqlText);
        Assert.Equal(DatabaseProvider.SQLite, sut.Tabs[1].Provider);
        Assert.Equal("profile-7", sut.Tabs[1].ConnectionProfileId);
        Assert.False(sut.Tabs[1].IsDirty);
    }

    [Fact]
    public void GetActiveTab_WhenNotInitialized_CreatesDefaultTab()
    {
        var sut = new SqlEditorTabManagerViewModel();

        SqlEditorTabState active = sut.GetActiveTab();

        Assert.Single(sut.Tabs);
        Assert.Same(active, sut.Tabs[0]);
        Assert.Equal(DatabaseProvider.Postgres, active.Provider);
        Assert.Equal("Script 1", active.FallbackTitle);
    }
}
