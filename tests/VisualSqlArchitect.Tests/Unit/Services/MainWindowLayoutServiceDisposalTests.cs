using VisualSqlArchitect.UI.Services;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Services;

public class MainWindowLayoutServiceDisposalTests
{
    [Fact]
    public void MainWindowLayoutService_ImplementsIDisposable()
    {
        Assert.True(typeof(MainWindowLayoutService).GetInterface(nameof(IDisposable)) is not null);
    }
}
