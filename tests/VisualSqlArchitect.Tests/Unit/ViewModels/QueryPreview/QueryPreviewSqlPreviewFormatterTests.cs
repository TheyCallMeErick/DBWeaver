using DBWeaver.Core;
using DBWeaver.UI.Services.QueryPreview;

namespace DBWeaver.Tests.Unit.ViewModels.QueryPreview;

public class QueryPreviewSqlPreviewFormatterTests
{
    [Fact]
    public void InlineBindingsForPreview_ReplacesAtAndColonPlaceholders()
    {
        var sut = new QueryPreviewSqlPreviewFormatter(DatabaseProvider.Postgres);
        var sql = "select * from t where id = @id and name = :name";
        var bindings = new Dictionary<string, object?>
        {
            ["id"] = 42,
            ["name"] = "alice"
        };

        string result = sut.InlineBindingsForPreview(sql, bindings);

        Assert.Contains("id = 42", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name = 'alice'", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InlineBindingsForPreview_FormatsBooleanByProvider()
    {
        var postgres = new QueryPreviewSqlPreviewFormatter(DatabaseProvider.Postgres);
        var sqlServer = new QueryPreviewSqlPreviewFormatter(DatabaseProvider.SqlServer);

        string pgResult = postgres.InlineBindingsForPreview("select * from t where is_active = @flag", new Dictionary<string, object?> { ["flag"] = true });
        string msResult = sqlServer.InlineBindingsForPreview("select * from t where is_active = @flag", new Dictionary<string, object?> { ["flag"] = true });

        Assert.Contains("TRUE", pgResult, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1", msResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InlineBindingsForPreview_EscapesSingleQuotes()
    {
        var sut = new QueryPreviewSqlPreviewFormatter(DatabaseProvider.Postgres);

        string result = sut.InlineBindingsForPreview(
            "select * from t where name = @name",
            new Dictionary<string, object?> { ["name"] = "O'Reilly" });

        Assert.Contains("'O''Reilly'", result, StringComparison.Ordinal);
    }
}

