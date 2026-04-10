using DBWeaver.UI.Services.SqlEditor;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class MutationGuardServiceTests
{
    [Fact]
    public void Analyze_NullOrWhitespace_ReturnsSafe()
    {
        var sut = new MutationGuardService();

        MutationGuardResult nullResult = sut.Analyze(null);
        MutationGuardResult whitespaceResult = sut.Analyze("   ");

        Assert.True(nullResult.IsSafe);
        Assert.False(nullResult.RequiresConfirmation);
        Assert.Empty(nullResult.Issues);
        Assert.True(whitespaceResult.IsSafe);
        Assert.False(whitespaceResult.RequiresConfirmation);
        Assert.Empty(whitespaceResult.Issues);
    }

    [Fact]
    public void Analyze_DeleteWithoutWhere_RequiresConfirmation()
    {
        var sut = new MutationGuardService();

        MutationGuardResult result = sut.Analyze("DELETE FROM orders;");

        Assert.False(result.IsSafe);
        Assert.True(result.RequiresConfirmation);
        Assert.Contains(result.Issues, i => i.Code == "NO_WHERE" && i.Severity == MutationGuardSeverity.Critical);
        Assert.Equal("SELECT COUNT(*) FROM orders", result.CountQuery);
    }

    [Fact]
    public void Analyze_DeleteWithTrivialWhere_RequiresConfirmation()
    {
        var sut = new MutationGuardService();

        MutationGuardResult result = sut.Analyze("DELETE FROM orders WHERE TRUE;");

        Assert.False(result.IsSafe);
        Assert.True(result.RequiresConfirmation);
        Assert.Contains(result.Issues, i => i.Code == "TRIVIAL_WHERE");
        Assert.Equal("SELECT COUNT(*) FROM orders WHERE TRUE", result.CountQuery);
    }

    [Fact]
    public void Analyze_UpdateWithTrivialWhere_RequiresConfirmation()
    {
        var sut = new MutationGuardService();

        MutationGuardResult result = sut.Analyze("UPDATE orders SET status = 'x' WHERE 1 = 1;");

        Assert.False(result.IsSafe);
        Assert.True(result.RequiresConfirmation);
        Assert.Contains(result.Issues, i => i.Code == "TRIVIAL_WHERE");
        Assert.Equal("SELECT COUNT(*) FROM orders WHERE 1 = 1", result.CountQuery);
    }

    [Fact]
    public void Analyze_UpdateWithoutWhere_RequiresConfirmation()
    {
        var sut = new MutationGuardService();

        MutationGuardResult result = sut.Analyze("UPDATE orders SET status = 'x';");

        Assert.False(result.IsSafe);
        Assert.True(result.RequiresConfirmation);
        Assert.Contains(result.Issues, i => i.Code == "NO_WHERE");
        Assert.Equal("SELECT COUNT(*) FROM orders", result.CountQuery);
    }

    [Fact]
    public void Analyze_InsertWithoutColumnList_ReturnsInfoOnly()
    {
        var sut = new MutationGuardService();

        MutationGuardResult result = sut.Analyze("INSERT INTO orders VALUES (1, 'x');");

        Assert.True(result.IsSafe);
        Assert.False(result.RequiresConfirmation);
        Assert.Contains(result.Issues, i => i.Code == "INSERT_WITHOUT_COLUMN_LIST" && i.Severity == MutationGuardSeverity.Info);
    }

    [Fact]
    public void Analyze_InsertWithColumnList_ReturnsSafe()
    {
        var sut = new MutationGuardService();

        MutationGuardResult result = sut.Analyze("INSERT INTO orders(id, status) VALUES (1, 'x');");

        Assert.True(result.IsSafe);
        Assert.False(result.RequiresConfirmation);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Analyze_Select_ReturnsSafe()
    {
        var sut = new MutationGuardService();

        MutationGuardResult result = sut.Analyze("SELECT * FROM orders WHERE id = 1;");

        Assert.True(result.IsSafe);
        Assert.False(result.RequiresConfirmation);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Analyze_DropTable_RequiresConfirmation()
    {
        var sut = new MutationGuardService();

        MutationGuardResult result = sut.Analyze("DROP TABLE orders;");

        Assert.False(result.IsSafe);
        Assert.True(result.RequiresConfirmation);
        Assert.Contains(result.Issues, i => i.Code == "DDL_MUTATION");
    }

    [Fact]
    public void Analyze_LocalizedMessages_UsesLocalizationValues()
    {
        var sut = new MutationGuardService(new FakeLocalizationService(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sqlEditor.guard.ddl.message"] = "DDL localized",
            ["sqlEditor.guard.ddl.recommendation"] = "Recommendation localized",
        }));

        MutationGuardResult result = sut.Analyze("TRUNCATE TABLE orders;");

        MutationGuardIssue issue = Assert.Single(result.Issues);
        Assert.Equal("DDL localized", issue.Message);
        Assert.Equal("Recommendation localized", issue.Suggestion);
    }

    private sealed class FakeLocalizationService(Dictionary<string, string> values) : DBWeaver.UI.Services.Localization.ILocalizationService
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }
        public string CurrentCulture => "en-US";
        public string CurrentLanguageLabel => "EN-US";
        public string this[string key] => values.TryGetValue(key, out string? value) ? value : key;
        public bool ToggleCulture() => false;
        public bool SetCulture(string culture) => false;
    }
}
