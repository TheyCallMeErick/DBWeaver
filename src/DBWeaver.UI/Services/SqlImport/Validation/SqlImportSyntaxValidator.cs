using DBWeaver.UI.Services.Localization;

namespace DBWeaver.UI.Services.SqlImport.Validation;

public sealed class SqlImportSyntaxValidator
{
    public void ValidateBasicSyntax(string sql)
    {
        if (TryFindUnterminatedSingleQuote(sql, out int quoteIndex))
        {
            (int line, int column) = GetLineAndColumn(sql, quoteIndex);
            throw new InvalidOperationException(
                string.Format(
                    LS("sqlImporter.error.syntaxUnterminatedString", "Syntax error at line {0}, column {1}: unterminated string literal."),
                    line,
                    column
                )
            );
        }

        if (TryFindUnmatchedParenthesis(sql, out int parenIndex, out bool missingClosing))
        {
            (int line, int column) = GetLineAndColumn(sql, parenIndex);
            string detail = missingClosing
                ? LS("sqlImporter.error.missingClosingParenthesis", "missing closing ')'")
                : LS("sqlImporter.error.unexpectedClosingParenthesis", "unexpected ')'");
            throw new InvalidOperationException(
                string.Format(
                    LS("sqlImporter.error.syntaxAtLineColumn", "Syntax error at line {0}, column {1}: {2}."),
                    line,
                    column,
                    detail
                )
            );
        }
    }

    private static bool TryFindUnterminatedSingleQuote(string sql, out int index)
    {
        bool inQuote = false;
        index = -1;

        for (int i = 0; i < sql.Length; i++)
        {
            if (sql[i] != '\'')
                continue;

            if (inQuote && i + 1 < sql.Length && sql[i + 1] == '\'')
            {
                i++;
                continue;
            }

            inQuote = !inQuote;
            if (inQuote)
                index = i;
        }

        return inQuote;
    }

    private static bool TryFindUnmatchedParenthesis(string sql, out int index, out bool missingClosing)
    {
        var stack = new Stack<int>();
        bool inQuote = false;
        index = -1;
        missingClosing = false;

        for (int i = 0; i < sql.Length; i++)
        {
            char ch = sql[i];

            if (ch == '\'')
            {
                if (inQuote && i + 1 < sql.Length && sql[i + 1] == '\'')
                {
                    i++;
                    continue;
                }

                inQuote = !inQuote;
                continue;
            }

            if (inQuote)
                continue;

            if (ch == '(')
            {
                stack.Push(i);
                continue;
            }

            if (ch == ')')
            {
                if (stack.Count == 0)
                {
                    index = i;
                    missingClosing = false;
                    return true;
                }

                stack.Pop();
            }
        }

        if (stack.Count > 0)
        {
            index = stack.Peek();
            missingClosing = true;
            return true;
        }

        return false;
    }

    private static (int line, int column) GetLineAndColumn(string text, int index)
    {
        int line = 1;
        int lineStart = 0;

        for (int i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        int column = Math.Max(1, index - lineStart + 1);
        return (line, column);
    }

    private static string LS(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
