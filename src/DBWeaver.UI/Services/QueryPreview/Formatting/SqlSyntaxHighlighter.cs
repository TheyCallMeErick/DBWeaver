
namespace DBWeaver.UI.Services.QueryPreview;

/// <summary>
/// Tokenizes SQL strings and classifies tokens for syntax highlighting.
/// Provides a simple regex-free approach that works well for visual highlighting.
/// </summary>
public sealed class SqlSyntaxHighlighter
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT",
        "FROM",
        "WHERE",
        "JOIN",
        "LEFT",
        "RIGHT",
        "INNER",
        "OUTER",
        "CROSS",
        "ON",
        "AND",
        "OR",
        "NOT",
        "AS",
        "DISTINCT",
        "GROUP",
        "BY",
        "ORDER",
        "HAVING",
        "LIMIT",
        "OFFSET",
        "BETWEEN",
        "LIKE",
        "IN",
        "IS",
        "NULL",
        "CASE",
        "WHEN",
        "THEN",
        "ELSE",
        "END",
        "EXISTS",
        "UNION",
        "ALL",
        "WITH",
        "FETCH",
        "ROWS",
        "ONLY",
        "ASC",
        "DESC",
        "TOP",
        "CAST",
        "CONVERT",
        "EXTRACT",
        "OVER",
        "PARTITION",
        "ISNULL",
        "COALESCE",
    };

    private static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        "UPPER",
        "LOWER",
        "TRIM",
        "LENGTH",
        "LEN",
        "CHAR_LENGTH",
        "SUBSTRING",
        "CONCAT",
        "ROUND",
        "ABS",
        "CEIL",
        "CEILING",
        "FLOOR",
        "SUM",
        "AVG",
        "COUNT",
        "MIN",
        "MAX",
        "YEAR",
        "MONTH",
        "DAY",
        "DATE_TRUNC",
        "DATE_DIFF",
        "DATEDIFF",
        "DATEADD",
        "JSON_VALUE",
        "JSON_QUERY",
        "JSON_EXTRACT",
        "PATINDEX",
        "STRING_AGG",
        "GROUP_CONCAT",
        "JSONB_ARRAY_LENGTH",
        "EXTRACT",
        "FORMAT",
        "CONVERT",
        "ISNULL",
        "IFNULL",
        "NULLIF",
        "GREATEST",
        "LEAST",
        "COALESCE",
        "CAST",
    };

    /// <summary>
    /// Tokenizes SQL into a collection of SqlToken objects for highlighting.
    /// </summary>
    public static void Tokenize(string sql, ObservableCollection<SqlToken> tokens)
    {
        tokens.Clear();
        if (string.IsNullOrWhiteSpace(sql))
            return;

        int i = 0;
        while (i < sql.Length)
        {
            char ch = sql[i];

            // Comment
            if (ch == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                int end = sql.IndexOf('\n', i);
                string text = end < 0 ? sql[i..] : sql[i..end];
                tokens.Add(new SqlToken(text, SqlTokenKind.Comment));
                i = end < 0 ? sql.Length : end;
                continue;
            }

            // String literal
            if (ch == '\'')
            {
                int j = i + 1;
                while (
                    j < sql.Length
                    && !(sql[j] == '\'' && (j + 1 >= sql.Length || sql[j + 1] != '\''))
                )
                    j++;
                j = Math.Min(j + 1, sql.Length);
                tokens.Add(new SqlToken(sql[i..j], SqlTokenKind.Literal));
                i = j;
                continue;
            }

            // Number
            if (char.IsDigit(ch) || (ch == '-' && i + 1 < sql.Length && char.IsDigit(sql[i + 1])))
            {
                int j = i + (ch == '-' ? 1 : 0);
                while (j < sql.Length && (char.IsDigit(sql[j]) || sql[j] == '.'))
                    j++;
                tokens.Add(new SqlToken(sql[i..j], SqlTokenKind.Literal));
                i = j;
                continue;
            }

            // Identifier / keyword / function
            if (char.IsLetter(ch) || ch == '_' || ch == '"' || ch == '[' || ch == '`')
            {
                char close = ch switch
                {
                    '"' => '"',
                    '[' => ']',
                    '`' => '`',
                    _ => '\0',
                };
                int j = i;
                if (close != '\0')
                {
                    j++;
                    while (j < sql.Length && sql[j] != close)
                        j++;
                    j = Math.Min(j + 1, sql.Length);
                }
                else
                {
                    while (
                        j < sql.Length
                        && (char.IsLetterOrDigit(sql[j]) || sql[j] == '_' || sql[j] == '.')
                    )
                        j++;
                }

                string word = sql[i..j];
                string bare = word.Trim('"', '[', ']', '`');

                // Check if next non-space is '(' â†’ function call
                int k = j;
                while (k < sql.Length && sql[k] == ' ')
                    k++;
                bool isCall = k < sql.Length && sql[k] == '(';

                SqlTokenKind kind =
                    (isCall && Functions.Contains(bare)) ? SqlTokenKind.Function
                    : Keywords.Contains(bare.Split('.')[0]) ? SqlTokenKind.Keyword
                    : SqlTokenKind.Identifier;

                tokens.Add(new SqlToken(word, kind));
                i = j;
                continue;
            }

            // Operators
            if (ch is '=' or '<' or '>' or '!' or '~')
            {
                int j = i + 1;
                if (j < sql.Length && sql[j] is '=' or '>' or '<')
                    j++;
                tokens.Add(new SqlToken(sql[i..j], SqlTokenKind.Operator));
                i = j;
                continue;
            }

            // Punctuation
            if (ch is '(' or ')' or ',' or ';' or '*')
            {
                tokens.Add(new SqlToken(ch.ToString(), SqlTokenKind.Punctuation));
                i++;
                continue;
            }

            // Plain (whitespace, newline, other)
            int ws = i;
            while (
                ws < sql.Length
                && !char.IsLetterOrDigit(sql[ws])
                && sql[ws]
                    is not '\''
                        and not '"'
                        and not '['
                        and not '`'
                        and not '-'
                        and not '='
                        and not '<'
                        and not '>'
                        and not '~'
                        and not '('
                        and not ')'
                        and not ','
                        and not ';'
                        and not '*'
            )
                ws++;
            if (ws > i)
            {
                tokens.Add(new SqlToken(sql[i..ws], SqlTokenKind.Plain));
                i = ws;
            }
            else
            {
                tokens.Add(new SqlToken(ch.ToString(), SqlTokenKind.Plain));
                i++;
            }
        }
    }
}



