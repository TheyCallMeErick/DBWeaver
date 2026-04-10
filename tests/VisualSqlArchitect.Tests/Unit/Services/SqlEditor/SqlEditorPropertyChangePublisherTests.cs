using DBWeaver.UI.Services.SqlEditor;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlEditorPropertyChangePublisherTests
{
    [Fact]
    public void PublishSqlPanelChanges_RaisesExpectedProperties()
    {
        var sut = new SqlEditorPropertyChangePublisher();
        var raised = new List<string>();

        sut.PublishSqlPanelChanges(raised.Add);

        Assert.Contains("ExecutionTelemetryText", raised);
        Assert.Contains("ResultSummaryText", raised);
        Assert.Contains("PendingMutationDiffText", raised);
        Assert.True(raised.Count >= 21);
    }

    [Fact]
    public void PublishTabStateChanges_RaisesExpectedProperties()
    {
        var sut = new SqlEditorPropertyChangePublisher();
        var raised = new List<string>();

        sut.PublishTabStateChanges(raised.Add);

        Assert.Contains("EditorTabs", raised);
        Assert.Contains("ActiveTab", raised);
        Assert.Contains("PendingCloseTabMessage", raised);
        Assert.Contains("ManyTabsWarningText", raised);
        Assert.True(raised.Count >= 7);
    }
}

