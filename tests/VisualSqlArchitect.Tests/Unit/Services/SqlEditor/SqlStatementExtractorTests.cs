using DBWeaver.UI.Services.SqlEditor;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlStatementExtractorTests
{
    [Fact]
    public void Extract_EmptyTokens_ReturnsEmptyContext()
    {
        var sut = new SqlStatementExtractor();

        SqlStatementContext context = sut.Extract([], caretOffset: 0);

        Assert.Empty(context.Tokens);
        Assert.Equal(0, context.StartOffset);
        Assert.Equal(0, context.EndOffset);
    }

    [Fact]
    public void Extract_WhenCaretInsideFirstStatement_ReturnsFirstStatementSlice()
    {
        const string sql = "SELECT 1; SELECT 2;";
        var tokenizer = new SqlTokenizer();
        var sut = new SqlStatementExtractor();

        SqlStatementContext context = sut.Extract(tokenizer.Tokenize(sql), caretOffset: 4);
        string statementText = sql[context.StartOffset..context.EndOffset].Trim();

        Assert.Equal("SELECT 1", statementText);
    }

    [Fact]
    public void Extract_WhenCaretInsideSecondStatement_ReturnsSecondStatementSlice()
    {
        const string sql = "SELECT 1; SELECT 2;";
        var tokenizer = new SqlTokenizer();
        var sut = new SqlStatementExtractor();

        SqlStatementContext context = sut.Extract(tokenizer.Tokenize(sql), caretOffset: 12);
        string statementText = sql[context.StartOffset..context.EndOffset].Trim();

        Assert.Equal("SELECT 2", statementText);
    }

    [Fact]
    public void Extract_WhenCaretOnSemicolon_ReturnsNextStatement()
    {
        const string sql = "SELECT 1; SELECT 2";
        int semicolonOffset = sql.IndexOf(';');
        var tokenizer = new SqlTokenizer();
        var sut = new SqlStatementExtractor();

        SqlStatementContext context = sut.Extract(tokenizer.Tokenize(sql), semicolonOffset);
        string statementText = sql[context.StartOffset..context.EndOffset];

        Assert.Contains("SELECT 2", statementText, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT 1", statementText, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_ExcludesCommentTokensFromStatementContext()
    {
        const string sql = "SELECT 1 -- hidden\nFROM users";
        var tokenizer = new SqlTokenizer();
        var sut = new SqlStatementExtractor();

        SqlStatementContext context = sut.Extract(tokenizer.Tokenize(sql), caretOffset: sql.Length);

        Assert.DoesNotContain(context.Tokens, t => t.IsComment);
        Assert.Contains(context.Tokens, t => t.Kind == SqlTokenKind.Keyword && string.Equals(t.Value, "FROM", StringComparison.OrdinalIgnoreCase));
    }
}
