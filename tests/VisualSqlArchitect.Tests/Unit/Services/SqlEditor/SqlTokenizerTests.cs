using DBWeaver.UI.Services.SqlEditor;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlTokenizerTests
{
    [Fact]
    public void Tokenize_EmptyInput_ReturnsEmptyList()
    {
        var sut = new SqlTokenizer();

        IReadOnlyList<SqlToken> tokens = sut.Tokenize(string.Empty);

        Assert.Empty(tokens);
    }

    [Fact]
    public void Tokenize_RecognizesKeywordsCaseInsensitive()
    {
        var sut = new SqlTokenizer();

        IReadOnlyList<SqlToken> tokens = sut.Tokenize("select * from users");

        Assert.Contains(tokens, t => t.Kind == SqlTokenKind.Keyword && string.Equals(t.Value, "select", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tokens, t => t.Kind == SqlTokenKind.Keyword && string.Equals(t.Value, "from", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Tokenize_LineComment_IsMarkedAsComment()
    {
        var sut = new SqlTokenizer();

        IReadOnlyList<SqlToken> tokens = sut.Tokenize("SELECT 1 -- comment\nFROM t");

        SqlToken lineComment = Assert.Single(tokens, t => t.Kind == SqlTokenKind.LineComment);
        Assert.True(lineComment.IsComment);
    }

    [Fact]
    public void Tokenize_BlockComment_IsMarkedAsComment()
    {
        var sut = new SqlTokenizer();

        IReadOnlyList<SqlToken> tokens = sut.Tokenize("SELECT /* comment */ 1");

        SqlToken blockComment = Assert.Single(tokens, t => t.Kind == SqlTokenKind.BlockComment);
        Assert.True(blockComment.IsComment);
    }

    [Fact]
    public void Tokenize_StringLiteralWithEscapedQuote_StaysSingleToken()
    {
        var sut = new SqlTokenizer();

        IReadOnlyList<SqlToken> tokens = sut.Tokenize("SELECT 'it''s ok'");

        SqlToken stringLiteral = Assert.Single(tokens, t => t.Kind == SqlTokenKind.StringLiteral);
        Assert.Equal("'it''s ok'", stringLiteral.Value);
    }

    [Fact]
    public void Tokenize_QuotedIdentifier_IsRecognized()
    {
        var sut = new SqlTokenizer();

        IReadOnlyList<SqlToken> tokens = sut.Tokenize("SELECT \"UserName\" FROM users");

        SqlToken quotedIdentifier = Assert.Single(tokens, t => t.Kind == SqlTokenKind.QuotedIdentifier);
        Assert.Equal("\"UserName\"", quotedIdentifier.Value);
    }

    [Fact]
    public void Tokenize_PunctuationAndOperators_AreRecognized()
    {
        var sut = new SqlTokenizer();

        IReadOnlyList<SqlToken> tokens = sut.Tokenize("SELECT (a+b) FROM t;");

        Assert.Contains(tokens, t => t.Kind == SqlTokenKind.Punctuation && t.Value == "(");
        Assert.Contains(tokens, t => t.Kind == SqlTokenKind.Operator && t.Value == "+");
        Assert.Contains(tokens, t => t.Kind == SqlTokenKind.Punctuation && t.Value == ")");
        Assert.Contains(tokens, t => t.Kind == SqlTokenKind.Punctuation && t.Value == ";");
    }

    [Fact]
    public void Tokenize_UnclosedBlockComment_DoesNotThrowAndProducesCommentToken()
    {
        var sut = new SqlTokenizer();

        IReadOnlyList<SqlToken> tokens = sut.Tokenize("SELECT /* comment");

        SqlToken token = Assert.Single(tokens, t => t.Kind == SqlTokenKind.BlockComment);
        Assert.True(token.IsComment);
    }

    [Fact]
    public void Tokenize_MixedQuotedIdentifierAndStringLiteral_SeparatesKinds()
    {
        var sut = new SqlTokenizer();

        IReadOnlyList<SqlToken> tokens = sut.Tokenize("SELECT \"name\", 'value' FROM users");

        Assert.Contains(tokens, t => t.Kind == SqlTokenKind.QuotedIdentifier && t.Value == "\"name\"");
        Assert.Contains(tokens, t => t.Kind == SqlTokenKind.StringLiteral && t.Value == "'value'");
    }
}
