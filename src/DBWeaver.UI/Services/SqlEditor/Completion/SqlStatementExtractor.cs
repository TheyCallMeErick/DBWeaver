namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlStatementExtractor
{
    public SqlStatementContext Extract(IReadOnlyList<SqlToken> tokens, int caretOffset)
    {
        if (tokens.Count == 0)
            return new SqlStatementContext([], 0, 0);

        int statementStart = 0;
        int statementEnd = tokens[^1].EndOffset;

        for (int i = 0; i < tokens.Count; i++)
        {
            SqlToken token = tokens[i];
            if (token.Kind != SqlTokenKind.Punctuation || token.Value != ";")
                continue;

            if (token.StartOffset <= caretOffset)
                statementStart = token.EndOffset;

            if (token.StartOffset > caretOffset)
            {
                statementEnd = token.StartOffset;
                break;
            }
        }

        IReadOnlyList<SqlToken> statementTokens = tokens
            .Where(token => token.StartOffset >= statementStart
                            && token.EndOffset <= statementEnd
                            && !token.IsComment)
            .ToList();

        return new SqlStatementContext(statementTokens, statementStart, statementEnd);
    }
}
