using System.Text;

namespace DBWeaver.Core;

/// <summary>
/// Splits SQL scripts into executable statements while respecting quoted literals
/// and comment blocks.
/// </summary>
public static class SqlStatementSplitter
{
    public static IReadOnlyList<string> Split(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return [];

        var statements = new List<string>();
        var sb = new StringBuilder();

        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        bool inLineComment = false;
        bool inBlockComment = false;

        for (int i = 0; i < sql.Length; i++)
        {
            char c = sql[i];
            char next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (inLineComment)
            {
                sb.Append(c);
                if (c == '\n')
                    inLineComment = false;
                continue;
            }

            if (inBlockComment)
            {
                sb.Append(c);
                if (c == '*' && next == '/')
                {
                    sb.Append(next);
                    i++;
                    inBlockComment = false;
                }

                continue;
            }

            if (!inSingleQuote && !inDoubleQuote)
            {
                if (c == '-' && next == '-')
                {
                    sb.Append(c);
                    sb.Append(next);
                    i++;
                    inLineComment = true;
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    sb.Append(c);
                    sb.Append(next);
                    i++;
                    inBlockComment = true;
                    continue;
                }
            }

            if (c == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                sb.Append(c);
                continue;
            }

            if (c == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                sb.Append(c);
                continue;
            }

            if (c == ';' && !inSingleQuote && !inDoubleQuote)
            {
                string statement = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(statement))
                    statements.Add(statement);
                sb.Clear();
                continue;
            }

            sb.Append(c);
        }

        string tail = sb.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(tail))
            statements.Add(tail);

        return statements;
    }
}
