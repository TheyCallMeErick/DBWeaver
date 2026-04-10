using System.Text.RegularExpressions;

namespace DBWeaver.UI.Services;

public static partial class SqlDisplayFormatter
{
    private static readonly string[] BreakKeywords =
    [
        "SELECT",
        "FROM",
        "WHERE",
        "GROUP BY",
        "HAVING",
        "ORDER BY",
        "LIMIT",
        "OFFSET",
        "JOIN",
        "LEFT JOIN",
        "RIGHT JOIN",
        "INNER JOIN",
        "FULL JOIN",
        "CROSS JOIN",
        "UNION",
        "UNION ALL",
    ];

    [GeneratedRegex("\\s+", RegexOptions.Compiled)]
    private static partial Regex MultiWhitespaceRegex();

    public static string Format(string sqlText)
    {
        if (string.IsNullOrWhiteSpace(sqlText))
            return string.Empty;

        string sql = MultiWhitespaceRegex().Replace(sqlText.Trim(), " ");

        foreach (string keyword in BreakKeywords.OrderByDescending(k => k.Length))
        {
            int idx = 0;
            while (idx < sql.Length)
            {
                idx = sql.IndexOf(keyword, idx, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    break;

                bool startOk = idx == 0 || char.IsWhiteSpace(sql[idx - 1]);
                bool endOk = idx + keyword.Length >= sql.Length || !char.IsLetterOrDigit(sql[idx + keyword.Length]);
                if (!startOk || !endOk)
                {
                    idx += keyword.Length;
                    continue;
                }

                string before = sql[..idx].TrimEnd();
                string after = sql[(idx + keyword.Length)..];
                sql = before + (before.Length > 0 ? "\n" : string.Empty) + keyword.ToUpperInvariant() + after;
                idx = before.Length + keyword.Length + 1;
            }
        }

        int selectEnd = sql.IndexOf("\nFROM", StringComparison.OrdinalIgnoreCase);
        if (sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) && selectEnd > 0)
        {
            string selectBody = sql[6..selectEnd].Trim();
            if (!string.IsNullOrWhiteSpace(selectBody))
            {
                string formatted = string.Join(",\n    ",
                    selectBody.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                );
                sql = "SELECT\n    " + formatted + sql[selectEnd..];
            }
        }

        return sql;
    }
}
