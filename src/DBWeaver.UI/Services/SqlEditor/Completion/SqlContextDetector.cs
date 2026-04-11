namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlContextDetector
{
    public SqlCompletionContext Detect(IReadOnlyList<SqlToken> statementTokens, int caretOffset)
    {
        if (statementTokens.Count == 0)
            return SqlCompletionContext.Unknown;

        List<SqlToken> nonWhitespaceTokens = statementTokens
            .Where(t => t.Kind != SqlTokenKind.Whitespace)
            .ToList();
        if (nonWhitespaceTokens.Count == 0)
            return SqlCompletionContext.Unknown;

        SqlCompletionContext context = SqlCompletionContext.Unknown;

        for (int i = 0; i < nonWhitespaceTokens.Count; i++)
        {
            SqlToken token = nonWhitespaceTokens[i];
            if (token.StartOffset >= caretOffset)
                break;

            if (token.Kind != SqlTokenKind.Keyword)
                continue;

            string keyword = token.Value.ToUpperInvariant();
            switch (keyword)
            {
                case "SELECT":
                    context = SqlCompletionContext.SelectList;
                    break;
                case "FROM":
                    context = SqlCompletionContext.FromClause;
                    break;
                case "JOIN":
                case "LEFT":
                case "RIGHT":
                case "INNER":
                case "FULL":
                case "CROSS":
                    context = SqlCompletionContext.JoinClause;
                    break;
                case "ON":
                    context = SqlCompletionContext.OnClause;
                    break;
                case "WHERE":
                    context = SqlCompletionContext.WhereClause;
                    break;
                case "HAVING":
                    context = SqlCompletionContext.HavingClause;
                    break;
                case "INTO":
                    context = SqlCompletionContext.InsertColumns;
                    break;
                case "VALUES":
                    context = SqlCompletionContext.ValuesClause;
                    break;
                case "SET":
                    context = SqlCompletionContext.UpdateSetClause;
                    break;
                case "ORDER":
                    if (HasNextBy(nonWhitespaceTokens, i))
                        context = SqlCompletionContext.OrderByClause;
                    break;
                case "GROUP":
                    if (HasNextBy(nonWhitespaceTokens, i))
                        context = SqlCompletionContext.GroupByClause;
                    break;
            }
        }

        return context;
    }

    private static bool HasNextBy(IReadOnlyList<SqlToken> tokens, int index)
    {
        if (index + 1 >= tokens.Count)
            return false;

        SqlToken next = tokens[index + 1];
        return next.Kind == SqlTokenKind.Keyword
               && string.Equals(next.Value, "BY", StringComparison.OrdinalIgnoreCase);
    }
}
