namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlTokenizer
{
    private static readonly HashSet<string> KeywordSet =
    [
        "SELECT", "FROM", "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "FULL", "CROSS", "ON",
        "WHERE", "GROUP", "ORDER", "BY", "HAVING", "INSERT", "INTO", "VALUES", "UPDATE", "SET",
        "DELETE", "AS", "WITH", "CASE", "END", "LIMIT", "OFFSET", "UNION",
    ];

    public IReadOnlyList<SqlToken> Tokenize(string fullText)
    {
        if (string.IsNullOrEmpty(fullText))
            return [];

        var tokens = new List<SqlToken>(capacity: Math.Min(fullText.Length, 2048));
        int i = 0;

        while (i < fullText.Length)
        {
            char c = fullText[i];

            if (char.IsWhiteSpace(c))
            {
                int start = i;
                while (i < fullText.Length && char.IsWhiteSpace(fullText[i]))
                    i++;
                tokens.Add(new SqlToken(SqlTokenKind.Whitespace, fullText[start..i], start, i, IsComment: false));
                continue;
            }

            if (c == '-' && i + 1 < fullText.Length && fullText[i + 1] == '-')
            {
                int start = i;
                i += 2;
                while (i < fullText.Length && fullText[i] is not ('\r' or '\n'))
                    i++;
                tokens.Add(new SqlToken(SqlTokenKind.LineComment, fullText[start..i], start, i, IsComment: true));
                continue;
            }

            if (c == '/' && i + 1 < fullText.Length && fullText[i + 1] == '*')
            {
                int start = i;
                i += 2;
                while (i + 1 < fullText.Length && !(fullText[i] == '*' && fullText[i + 1] == '/'))
                    i++;

                if (i + 1 < fullText.Length)
                    i += 2;
                else
                    i = fullText.Length;

                tokens.Add(new SqlToken(SqlTokenKind.BlockComment, fullText[start..i], start, i, IsComment: true));
                continue;
            }

            if (c == '\'')
            {
                int start = i;
                i++;
                while (i < fullText.Length)
                {
                    if (fullText[i] == '\'' && i + 1 < fullText.Length && fullText[i + 1] == '\'')
                    {
                        i += 2;
                        continue;
                    }

                    if (fullText[i] == '\'')
                    {
                        i++;
                        break;
                    }

                    i++;
                }

                tokens.Add(new SqlToken(SqlTokenKind.StringLiteral, fullText[start..i], start, i, IsComment: false));
                continue;
            }

            if (c == '"')
            {
                int start = i;
                i++;
                while (i < fullText.Length && fullText[i] != '"')
                    i++;

                if (i < fullText.Length)
                    i++;

                tokens.Add(new SqlToken(SqlTokenKind.QuotedIdentifier, fullText[start..i], start, i, IsComment: false));
                continue;
            }

            if (char.IsDigit(c))
            {
                int start = i;
                i++;
                while (i < fullText.Length && (char.IsDigit(fullText[i]) || fullText[i] == '.'))
                    i++;

                tokens.Add(new SqlToken(SqlTokenKind.NumericLiteral, fullText[start..i], start, i, IsComment: false));
                continue;
            }

            if (IsIdentifierStart(c))
            {
                int start = i;
                i++;
                while (i < fullText.Length && IsIdentifierPart(fullText[i]))
                    i++;

                string value = fullText[start..i];
                SqlTokenKind kind = KeywordSet.Contains(value.ToUpperInvariant())
                    ? SqlTokenKind.Keyword
                    : SqlTokenKind.Identifier;
                tokens.Add(new SqlToken(kind, value, start, i, IsComment: false));
                continue;
            }

            if (IsPunctuation(c))
            {
                int start = i;
                i++;
                tokens.Add(new SqlToken(SqlTokenKind.Punctuation, fullText[start..i], start, i, IsComment: false));
                continue;
            }

            if (IsOperator(c))
            {
                int start = i;
                i++;
                tokens.Add(new SqlToken(SqlTokenKind.Operator, fullText[start..i], start, i, IsComment: false));
                continue;
            }

            int unknownStart = i;
            i++;
            tokens.Add(new SqlToken(SqlTokenKind.Unknown, fullText[unknownStart..i], unknownStart, i, IsComment: false));
        }

        return tokens;
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c is '_' or '$';

    private static bool IsPunctuation(char c) => c is ',' or ';' or '.' or '(' or ')' or '[' or ']' or '{' or '}';

    private static bool IsOperator(char c) => c is '+' or '-' or '*' or '/' or '=' or '<' or '>' or '!' or '%' or '&' or '|';
}
