using DBWeaver.UI.Services.SqlEditor;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlContextDetectorTests
{
    [Theory]
    [InlineData("SELECT id, na|", SqlCompletionContext.SelectList)]
    [InlineData("SELECT * FROM users u WHERE u.created_at > NOW() AND u.|", SqlCompletionContext.WhereClause)]
    [InlineData("SELECT * FROM users WHERE u.|", SqlCompletionContext.WhereClause)]
    [InlineData("SELECT * FROM users u JOIN orders o ON u.|", SqlCompletionContext.OnClause)]
    [InlineData("INSERT INTO users (|", SqlCompletionContext.InsertColumns)]
    [InlineData("INSERT INTO users (id, name) VALUES (|", SqlCompletionContext.ValuesClause)]
    [InlineData("UPDATE users SET name|", SqlCompletionContext.UpdateSetClause)]
    [InlineData("SELECT id FROM users GROUP BY |", SqlCompletionContext.GroupByClause)]
    [InlineData("SELECT id FROM users ORDER BY |", SqlCompletionContext.OrderByClause)]
    [InlineData("SELECT id FROM users HAVING |", SqlCompletionContext.HavingClause)]
    [InlineData("SELECT * FROM users u LEFT JOIN orders o ON |", SqlCompletionContext.OnClause)]
    [InlineData("SELECT * FROM users u RIGHT JOIN orders o ON |", SqlCompletionContext.OnClause)]
    [InlineData("SELECT * FROM users u INNER JOIN orders o ON |", SqlCompletionContext.OnClause)]
    [InlineData("SELECT * FROM users u FULL JOIN orders o ON |", SqlCompletionContext.OnClause)]
    [InlineData("SELECT * FROM users u JOIN |", SqlCompletionContext.JoinClause)]
    [InlineData("SELECT * FROM |", SqlCompletionContext.FromClause)]
    [InlineData("SELECT * FROM users WHERE id = 1 GROUP BY id HAVING count(*) > 0 ORDER BY |", SqlCompletionContext.OrderByClause)]
    [InlineData("UPDATE users SET name = 'x', email = |", SqlCompletionContext.UpdateSetClause)]
    [InlineData("INSERT INTO users (id, name) VALUES (1, |", SqlCompletionContext.ValuesClause)]
    [InlineData("WITH cte AS (SELECT * FROM users) SELECT * FROM cte WHERE |", SqlCompletionContext.WhereClause)]
    [InlineData("SELECT id FROM users WHERE active = 1 AND |", SqlCompletionContext.WhereClause)]
    [InlineData("SELECT id FROM users WHERE active = 1 OR |", SqlCompletionContext.WhereClause)]
    [InlineData("SELECT id FROM users GROUP BY id, |", SqlCompletionContext.GroupByClause)]
    [InlineData("SELECT id FROM users ORDER BY id DESC, |", SqlCompletionContext.OrderByClause)]
    [InlineData("SELECT id FROM users HAVING count(*) > 1 AND |", SqlCompletionContext.HavingClause)]
    [InlineData("SELECT * FROM users u CROSS JOIN orders o ON |", SqlCompletionContext.OnClause)]
    [InlineData("SELECT * FROM users u CROSS JOIN |", SqlCompletionContext.JoinClause)]
    [InlineData("SELECT * FROM users u LEFT JOIN orders o ON u.id = o.user_id WHERE |", SqlCompletionContext.WhereClause)]
    [InlineData("SELECT * FROM users u RIGHT JOIN orders o ON u.id = o.user_id WHERE |", SqlCompletionContext.WhereClause)]
    [InlineData("SELECT * FROM users u INNER JOIN orders o ON u.id = o.user_id WHERE |", SqlCompletionContext.WhereClause)]
    [InlineData("SELECT * FROM users u FULL JOIN orders o ON u.id = o.user_id WHERE |", SqlCompletionContext.WhereClause)]
    [InlineData("SELECT * FROM users u JOIN orders o ON u.id = o.user_id ORDER BY |", SqlCompletionContext.OrderByClause)]
    [InlineData("INSERT INTO public.users |", SqlCompletionContext.InsertColumns)]
    [InlineData("INSERT INTO users(id) VALUES(1),(|", SqlCompletionContext.ValuesClause)]
    [InlineData("INSERT INTO users (id, name) VALUES (1, 'a'), (2, |", SqlCompletionContext.ValuesClause)]
    [InlineData("UPDATE users SET id = 1 WHERE |", SqlCompletionContext.WhereClause)]
    [InlineData("UPDATE users SET id = 1, name = 'x' WHERE id > 0 AND |", SqlCompletionContext.WhereClause)]
    [InlineData("WITH cte AS (SELECT * FROM users) SELECT |", SqlCompletionContext.SelectList)]
    [InlineData("WITH cte AS (SELECT * FROM users) SELECT * FROM cte JOIN |", SqlCompletionContext.JoinClause)]
    [InlineData("WITH cte AS (SELECT * FROM users) SELECT * FROM cte ORDER BY |", SqlCompletionContext.OrderByClause)]
    [InlineData("SELECT CASE WHEN a = 1 THEN b ELSE c END FROM users WHERE |", SqlCompletionContext.WhereClause)]
    [InlineData("SELECT * FROM users -- comment\nWHERE |", SqlCompletionContext.WhereClause)]
    [InlineData("SELECT * /* comment with FROM */ FROM users WHERE |", SqlCompletionContext.WhereClause)]
    [InlineData("SELECT * FROM users; SELECT * FROM orders WHERE |", SqlCompletionContext.WhereClause)]
    [InlineData("SELECT * FROM users; INSERT INTO orders (id) VALUES (|", SqlCompletionContext.ValuesClause)]
    [InlineData("SELECT * FROM users; UPDATE orders SET status = 'x' WHERE |", SqlCompletionContext.WhereClause)]
    [InlineData("DELETE FROM users WHERE |", SqlCompletionContext.WhereClause)]
    [InlineData("DELETE FROM users |", SqlCompletionContext.FromClause)]
    [InlineData("SELECT * FROM users ORDER BY created_at, id, |", SqlCompletionContext.OrderByClause)]
    [InlineData("SELECT * FROM users GROUP BY country, city, |", SqlCompletionContext.GroupByClause)]
    [InlineData("SELECT * FROM users HAVING SUM(score) > 0 OR |", SqlCompletionContext.HavingClause)]
    [InlineData("SELECT * FROM users u JOIN orders o ON u.id = o.user_id HAVING |", SqlCompletionContext.HavingClause)]
    public void Detect_ReturnsExpectedContext_ForCommonCases(string sourceWithCaret, SqlCompletionContext expected)
    {
        (string sql, int caretOffset) = ParseCaret(sourceWithCaret);
        var tokenizer = new SqlTokenizer();
        var extractor = new SqlStatementExtractor();
        var detector = new SqlContextDetector();

        IReadOnlyList<SqlToken> tokens = tokenizer.Tokenize(sql);
        SqlStatementContext statement = extractor.Extract(tokens, caretOffset);

        SqlCompletionContext actual = detector.Detect(statement.Tokens, caretOffset);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Detect_IgnoresKeywordInsideComments()
    {
        const string source = "-- FROM users\nSELECT |";
        (string sql, int caretOffset) = ParseCaret(source);

        var tokenizer = new SqlTokenizer();
        var extractor = new SqlStatementExtractor();
        var detector = new SqlContextDetector();

        SqlCompletionContext actual = detector.Detect(
            extractor.Extract(tokenizer.Tokenize(sql), caretOffset).Tokens,
            caretOffset);

        Assert.Equal(SqlCompletionContext.SelectList, actual);
    }

    [Fact]
    public void Detect_IgnoresKeywordInsideBlockComments()
    {
        const string source = "/* FROM users */ SELECT |";
        (string sql, int caretOffset) = ParseCaret(source);

        var tokenizer = new SqlTokenizer();
        var extractor = new SqlStatementExtractor();
        var detector = new SqlContextDetector();

        SqlCompletionContext actual = detector.Detect(
            extractor.Extract(tokenizer.Tokenize(sql), caretOffset).Tokens,
            caretOffset);

        Assert.Equal(SqlCompletionContext.SelectList, actual);
    }

    [Fact]
    public void Extract_UsesStatementThatContainsCaret_InMultiStatementScript()
    {
        const string source = "SELECT * FROM a; SELECT |";
        (string sql, int caretOffset) = ParseCaret(source);

        var tokenizer = new SqlTokenizer();
        var extractor = new SqlStatementExtractor();
        var detector = new SqlContextDetector();

        SqlStatementContext statement = extractor.Extract(tokenizer.Tokenize(sql), caretOffset);
        SqlCompletionContext actual = detector.Detect(statement.Tokens, caretOffset);

        Assert.Equal(SqlCompletionContext.SelectList, actual);
    }

    [Fact]
    public void Extract_WhenCaretIsOnSemicolon_UsesNextStatement()
    {
        const string source = "SELECT 1;| SELECT 2";
        (string sql, int caretOffset) = ParseCaret(source);

        var tokenizer = new SqlTokenizer();
        var extractor = new SqlStatementExtractor();

        SqlStatementContext statement = extractor.Extract(tokenizer.Tokenize(sql), caretOffset);
        string statementText = sql[statement.StartOffset..statement.EndOffset];

        Assert.Contains("SELECT 2", statementText, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT 1", statementText, StringComparison.Ordinal);
    }

    [Fact]
    public void Detect_ReturnsUnknown_ForWhitespaceOnlyStatement()
    {
        const string source = "   |   ";
        (string sql, int caretOffset) = ParseCaret(source);

        var tokenizer = new SqlTokenizer();
        var extractor = new SqlStatementExtractor();
        var detector = new SqlContextDetector();

        SqlCompletionContext actual = detector.Detect(
            extractor.Extract(tokenizer.Tokenize(sql), caretOffset).Tokens,
            caretOffset);

        Assert.Equal(SqlCompletionContext.Unknown, actual);
    }

    [Fact]
    public void Detect_UsesSecondStatement_WhenFirstHasKeywordsInComments()
    {
        const string source = "-- SELECT FROM WHERE\nSELECT 1; SELECT * FROM users WHERE |";
        (string sql, int caretOffset) = ParseCaret(source);

        var tokenizer = new SqlTokenizer();
        var extractor = new SqlStatementExtractor();
        var detector = new SqlContextDetector();

        SqlStatementContext statement = extractor.Extract(tokenizer.Tokenize(sql), caretOffset);
        SqlCompletionContext actual = detector.Detect(statement.Tokens, caretOffset);

        Assert.Equal(SqlCompletionContext.WhereClause, actual);
    }

    [Fact]
    public void Detect_WhenCaretBeforeAnyKeyword_ReturnsUnknown()
    {
        const string source = "|SELECT id FROM users";
        (string sql, int caretOffset) = ParseCaret(source);

        var tokenizer = new SqlTokenizer();
        var extractor = new SqlStatementExtractor();
        var detector = new SqlContextDetector();

        SqlCompletionContext actual = detector.Detect(
            extractor.Extract(tokenizer.Tokenize(sql), caretOffset).Tokens,
            caretOffset);

        Assert.Equal(SqlCompletionContext.Unknown, actual);
    }

    private static (string Sql, int CaretOffset) ParseCaret(string source)
    {
        int caretOffset = source.IndexOf('|');
        Assert.True(caretOffset >= 0, "Test source must include a | marker for caret position.");

        string sql = source.Remove(caretOffset, 1);
        return (sql, caretOffset);
    }
}
