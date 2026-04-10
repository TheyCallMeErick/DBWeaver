using DBWeaver.UI.Services;
using Xunit;

namespace DBWeaver.Tests.Unit.Services;

public class MainWindowLayoutServiceDisposalTests
{
    [Fact]
    public void MainWindowLayoutService_ImplementsIDisposable()
    {
        Assert.True(typeof(MainWindowLayoutService).GetInterface(nameof(IDisposable)) is not null);
    }
}
