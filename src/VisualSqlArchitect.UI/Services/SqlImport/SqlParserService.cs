using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace VisualSqlArchitect.UI.Services.SqlImport;

public sealed record SqlParseResult(
    bool Success,
    string? NormalizedSql,
    string? ErrorMessage = null,
    int? ErrorLine = null,
    int? ErrorColumn = null
)
{
    public string ToUserMessage() =>
        Success
            ? "SQL parsed successfully."
            : $"{ErrorMessage}"
                + (ErrorLine.HasValue && ErrorColumn.HasValue
                    ? $" (line {ErrorLine.Value}, column {ErrorColumn.Value})"
                    : string.Empty);
}

public sealed class SqlParserService
{
    public SqlParseResult Parse(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return new SqlParseResult(false, null, "SQL is empty.", 1, 1);

        string normalized = Normalize(sql);

        if (!Regex.IsMatch(normalized, @"^(SELECT|WITH)\b", RegexOptions.IgnoreCase))
            return new SqlParseResult(false, null, "Only SELECT/WITH statements are supported.", 1, 1);

        if (TryFindUnterminatedSingleQuote(sql, out int quoteIndex))
        {
            (int line, int column) = GetLineAndColumn(sql, quoteIndex);
            return new SqlParseResult(
                false,
                null,
                "Syntax error: unterminated string literal.",
                line,
                column
            );
        }

        if (TryFindUnmatchedParenthesis(sql, out int parenIndex, out bool missingClosing))
        {
            (int line, int column) = GetLineAndColumn(sql, parenIndex);
            string detail = missingClosing ? "missing closing ')'" : "unexpected ')'";
            return new SqlParseResult(false, null, $"Syntax error: {detail}.", line, column);
        }

        // AST-first for SQL Server syntax; when parser cannot consume non-TSQL constructs
        // (e.g., LIMIT), keep resilient fallback to normalized SQL.
        SqlParseResult astResult = TryParseWithSqlServerAst(normalized);
        if (astResult.Success)
            return astResult;

        if (LooksLikeDialectVariant(normalized))
            return new SqlParseResult(true, normalized);

        return astResult;
    }

    private static SqlParseResult TryParseWithSqlServerAst(string sql)
    {
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql);
        IList<ParseError> errors;
        _ = parser.Parse(reader, out errors);

        if (errors.Count == 0)
            return new SqlParseResult(true, sql);

        ParseError first = errors[0];
        string message = string.IsNullOrWhiteSpace(first.Message)
            ? "Syntax error."
            : first.Message;

        return new SqlParseResult(false, null, message, first.Line, first.Column);
    }

    private static bool LooksLikeDialectVariant(string sql)
    {
        return Regex.IsMatch(sql, @"\bLIMIT\b|\bILIKE\b|\bRETURNING\b", RegexOptions.IgnoreCase);
    }

    private static string Normalize(string sql)
    {
        string normalized = Regex.Replace(sql, @"--[^\n]*", " ");
        normalized = Regex.Replace(normalized, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized;
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
}
