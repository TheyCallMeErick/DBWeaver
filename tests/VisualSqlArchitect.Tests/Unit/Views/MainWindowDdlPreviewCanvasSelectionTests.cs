using System.Reflection;
using DBWeaver.UI;
using Xunit;

namespace DBWeaver.Tests.Unit.Views;

public class MainWindowDdlPreviewCanvasSelectionTests
{
    [Fact]
    public void ResolveDdlSqlSourceCanvas_MethodWasRemoved_AfterDocumentIsolationRefactor()
    {
        MethodInfo? method = typeof(MainWindow).GetMethod(
            "ResolveDdlSqlSourceCanvas",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.Null(method);
    }
}
