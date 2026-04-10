using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.Services.Benchmark;

using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels;

public class QueryTabManagerViewModelTests
{
    [Fact]
    public void Initialize_StoresExplainHistoryInFirstTab()
    {
        var sut = new QueryTabManagerViewModel();
        var history = new List<ExplainHistoryState>
        {
            new(DateTimeOffset.UtcNow, "Seq Scan", 98, 1),
        };

        sut.Initialize("{}", "file.vsa", true, history);

        QueryTabState tab = Assert.Single(sut.Tabs);
        Assert.Single(tab.ExplainHistory);
        Assert.Equal("Seq Scan", tab.ExplainHistory[0].TopOperation);
    }

    [Fact]
    public void CaptureActive_UpdatesExplainHistory()
    {
        var sut = new QueryTabManagerViewModel();
        sut.Initialize("{}", null, false, null);

        var history = new List<ExplainHistoryState>
        {
            new(DateTimeOffset.UtcNow, "Hash Join", 203, 2),
        };

        sut.CaptureActive("{a}", "q.vsa", true, history);

        QueryTabState tab = Assert.Single(sut.Tabs);
        Assert.Equal("{a}", tab.SnapshotJson);
        Assert.Single(tab.ExplainHistory);
        Assert.Equal("Hash Join", tab.ExplainHistory[0].TopOperation);
    }
}


