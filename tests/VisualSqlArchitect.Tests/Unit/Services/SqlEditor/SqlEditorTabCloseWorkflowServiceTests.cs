using DBWeaver.Core;
using DBWeaver.UI.Services.SqlEditor;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlEditorTabCloseWorkflowServiceTests
{
    [Fact]
    public void RequestClose_WhenDirty_ReturnsConfirmationRequiredAndSetsPending()
    {
        var sut = new SqlEditorTabCloseWorkflowService();
        SqlEditorTabManagerViewModel tabs = BuildTabs(2);
        tabs.Tabs[1].IsDirty = true;

        SqlEditorTabCloseOutcome outcome = sut.RequestClose(tabs, tabs.Tabs[1].Id);

        Assert.Equal(SqlEditorTabCloseAction.ConfirmationRequired, outcome.Action);
        Assert.True(sut.HasPendingConfirmation);
        AssertLocalized(
            outcome.StatusText,
            "O fechamento da aba exige confirmacao.",
            "Tab close requires confirmation.");
        Assert.Equal(2, tabs.Tabs.Count);
    }

    [Fact]
    public void ConfirmClose_WhenPending_ClosesTab()
    {
        var sut = new SqlEditorTabCloseWorkflowService();
        SqlEditorTabManagerViewModel tabs = BuildTabs(2);
        tabs.Tabs[1].IsDirty = true;
        _ = sut.RequestClose(tabs, tabs.Tabs[1].Id);

        SqlEditorTabCloseOutcome outcome = sut.ConfirmClose(tabs);

        Assert.Equal(SqlEditorTabCloseAction.Closed, outcome.Action);
        Assert.False(sut.HasPendingConfirmation);
        Assert.Single(tabs.Tabs);
        AssertLocalized(outcome.StatusText, "Aba fechada.", "Tab closed.");
    }

    [Fact]
    public void CancelClose_WhenPending_ClearsPendingWithoutClosing()
    {
        var sut = new SqlEditorTabCloseWorkflowService();
        SqlEditorTabManagerViewModel tabs = BuildTabs(2);
        tabs.Tabs[1].IsDirty = true;
        _ = sut.RequestClose(tabs, tabs.Tabs[1].Id);

        SqlEditorTabCloseOutcome outcome = sut.CancelClose();

        Assert.Equal(SqlEditorTabCloseAction.None, outcome.Action);
        Assert.False(sut.HasPendingConfirmation);
        AssertLocalized(outcome.StatusText, "Fechamento da aba cancelado.", "Tab close canceled.");
        Assert.Equal(2, tabs.Tabs.Count);
    }

    [Fact]
    public void RequestClose_WhenClean_ClosesImmediately()
    {
        var sut = new SqlEditorTabCloseWorkflowService();
        SqlEditorTabManagerViewModel tabs = BuildTabs(2);

        SqlEditorTabCloseOutcome outcome = sut.RequestClose(tabs, tabs.Tabs[1].Id);

        Assert.Equal(SqlEditorTabCloseAction.Closed, outcome.Action);
        Assert.Single(tabs.Tabs);
    }

    [Fact]
    public void CanCloseTab_RequiresExistingTabAndAtLeastTwoTabs()
    {
        var sut = new SqlEditorTabCloseWorkflowService();
        SqlEditorTabManagerViewModel tabs = BuildTabs(1);

        Assert.False(sut.CanCloseTab(tabs, tabs.Tabs[0].Id));
        tabs.AddNewTab();
        Assert.True(sut.CanCloseTab(tabs, tabs.Tabs[1].Id));
        Assert.False(sut.CanCloseTab(tabs, "missing"));
    }

    private static SqlEditorTabManagerViewModel BuildTabs(int count)
    {
        var tabs = new SqlEditorTabManagerViewModel();
        tabs.Initialize(DatabaseProvider.Postgres);
        for (int i = 1; i < count; i++)
            tabs.AddNewTab();
        return tabs;
    }

    private static void AssertLocalized(string? actual, params string[] expectedValues)
    {
        Assert.NotNull(actual);
        Assert.Contains(actual!, expectedValues);
    }
}
