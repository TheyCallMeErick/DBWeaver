using DBWeaver.Core;

namespace DBWeaver.Tests.Unit.Core;

public sealed class SqlStatementSplitterTests
{
    [Fact]
    public void Split_SingleStatement_ReturnsSingleItem()
    {
        IReadOnlyList<string> result = SqlStatementSplitter.Split("SELECT 1;");
        Assert.Single(result);
        Assert.Equal("SELECT 1", result[0]);
    }

    [Fact]
    public void Split_TwoStatementsSemicolon_ReturnsTwoItems()
    {
        IReadOnlyList<string> result = SqlStatementSplitter.Split("SELECT 1; SELECT 2;");
        Assert.Equal(2, result.Count);
        Assert.Equal("SELECT 1", result[0]);
        Assert.Equal("SELECT 2", result[1]);
    }

    [Fact]
    public void Split_EmptyOrWhitespace_ReturnsEmpty()
    {
        Assert.Empty(SqlStatementSplitter.Split(string.Empty));
        Assert.Empty(SqlStatementSplitter.Split("   \n\t  "));
    }

    [Fact]
    public void Split_SemicolonInsideSingleQuote_DoesNotSplit()
    {
        IReadOnlyList<string> result = SqlStatementSplitter.Split("SELECT 'a;b'; SELECT 2;");
        Assert.Equal(2, result.Count);
        Assert.Equal("SELECT 'a;b'", result[0]);
    }

    [Fact]
    public void Split_SemicolonInsideDoubleQuote_DoesNotSplit()
    {
        IReadOnlyList<string> result = SqlStatementSplitter.Split("SELECT \"a;b\"; SELECT 2;");
        Assert.Equal(2, result.Count);
        Assert.Equal("SELECT \"a;b\"", result[0]);
    }

    [Fact]
    public void Split_SemicolonInsideLineComment_DoesNotSplit()
    {
        IReadOnlyList<string> result = SqlStatementSplitter.Split("SELECT 1 -- keep;comment\n; SELECT 2;");
        Assert.Equal(2, result.Count);
        Assert.Contains("-- keep;comment", result[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Split_SemicolonInsideBlockComment_DoesNotSplit()
    {
        IReadOnlyList<string> result = SqlStatementSplitter.Split("SELECT 1 /* x;y */; SELECT 2;");
        Assert.Equal(2, result.Count);
        Assert.Contains("/* x;y */", result[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Split_WithoutTerminatingSemicolon_IncludesTailStatement()
    {
        IReadOnlyList<string> result = SqlStatementSplitter.Split("SELECT 1; SELECT 2");
        Assert.Equal(2, result.Count);
        Assert.Equal("SELECT 2", result[1]);
    }
}
