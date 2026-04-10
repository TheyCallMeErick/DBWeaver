using DBWeaver.UI.Services;

namespace DBWeaver.Tests.Unit.Services;

public class SqlDisplayFormatterTests
{
    [Fact]
    public void Format_BreaksMainClausesAndUppercasesKeywords()
    {
        string formatted = SqlDisplayFormatter.Format("select id,name from users where active = 1 order by name");

        Assert.Contains("SELECT", formatted);
        Assert.Contains("\nFROM", formatted);
        Assert.Contains("\nWHERE", formatted);
        Assert.Contains("\nORDER BY", formatted);
    }

    [Fact]
    public void Format_ExpandsSelectListWithIndentation()
    {
        string formatted = SqlDisplayFormatter.Format("select id, name, created_at from users");

        Assert.Contains("SELECT\n    id,\n    name,\n    created_at\nFROM", formatted);
    }
}
