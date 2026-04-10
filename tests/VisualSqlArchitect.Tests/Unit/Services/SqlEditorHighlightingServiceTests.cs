using DBWeaver.UI.Services;

namespace DBWeaver.Tests.Unit.Services;

public class SqlEditorHighlightingServiceTests
{
    [Fact]
    public void GetSqlDefinition_IsStableAcrossCalls()
    {
        var first = SqlEditorHighlightingService.GetSqlDefinition();
        var second = SqlEditorHighlightingService.GetSqlDefinition();

        Assert.Same(first, second);
    }
}
