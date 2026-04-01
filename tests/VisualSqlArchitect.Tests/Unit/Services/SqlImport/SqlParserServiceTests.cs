using VisualSqlArchitect.UI.Services.SqlImport;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Services.SqlImport;

public class SqlParserServiceTests
{
    [Fact]
    public void Parse_WithSimpleSelect_ReturnsNormalizedSqlSuccessfully()
    {
        var parser = new SqlParserService();

        SqlParseResult result = parser.Parse("SELECT id FROM orders");

        Assert.True(result.Success);
        Assert.Equal("SELECT id FROM orders", result.NormalizedSql);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithJoinQuery_ReturnsNormalizedSqlSuccessfully()
    {
        var parser = new SqlParserService();

        SqlParseResult result = parser.Parse(
            "SELECT o.id, c.name FROM orders o INNER JOIN customers c ON o.customer_id = c.id"
        );

        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.NormalizedSql));
        Assert.Contains("JOIN", result.NormalizedSql!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithSyntaxError_ReturnsReadableMessageWithLineAndColumn()
    {
        var parser = new SqlParserService();

        SqlParseResult result = parser.Parse("SELECT id FROM orders WHERE note = 'abc");

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.True(result.ErrorLine.HasValue);
        Assert.True(result.ErrorColumn.HasValue);
    }

    [Fact]
    public void Parse_WithSqlServerAstFriendlySelect_Succeeds()
    {
        var parser = new SqlParserService();

        SqlParseResult result = parser.Parse("SELECT TOP 10 o.id FROM dbo.orders o ORDER BY o.id DESC");

        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.NormalizedSql));
    }

    [Fact]
    public void Parse_WithLimitDialectVariant_UsesSafeFallbackAndSucceeds()
    {
        var parser = new SqlParserService();

        SqlParseResult result = parser.Parse("SELECT id FROM orders LIMIT 5");

        Assert.True(result.Success);
        Assert.Contains("LIMIT", result.NormalizedSql!, StringComparison.OrdinalIgnoreCase);
    }
}
