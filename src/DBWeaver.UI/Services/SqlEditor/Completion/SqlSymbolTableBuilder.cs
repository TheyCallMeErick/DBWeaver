using DBWeaver.Core;
using System.Text.RegularExpressions;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlSymbolTableBuilder
{
    private static readonly SqlTokenizer Tokenizer = new();

    public SqlSymbolTable Build(string statementBeforeCaret, DatabaseProvider provider)
    {
        string source = statementBeforeCaret ?? string.Empty;
        IReadOnlyList<SqlToken> tokens = Tokenizer.Tokenize(source);
        List<SqlToken> significant = tokens
            .Where(static t => t.Kind is not (SqlTokenKind.Whitespace or SqlTokenKind.LineComment or SqlTokenKind.BlockComment))
            .ToList();

        var cteNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bindings = new List<SqlTableBindingSymbol>();

        ParseCteDefinitions(significant, cteNames);
        ParseFromAndJoinBindings(significant, cteNames, bindings);
        ApplyProviderFallbackForQuotedSubqueryAliases(source, provider, cteNames, bindings);

        return new SqlSymbolTable(bindings.OrderBy(static b => b.Order).ToList(), cteNames);
    }

    private static void ParseCteDefinitions(IReadOnlyList<SqlToken> tokens, ISet<string> cteNames)
    {
        int i = 0;
        if (!TryGetKeyword(tokens, i, "WITH"))
            return;

        i++;
        while (i < tokens.Count)
        {
            if (!TryReadSingleIdentifier(tokens, ref i, DatabaseProvider.Postgres, out string? cteName) || string.IsNullOrWhiteSpace(cteName))
                break;

            cteNames.Add(cteName);

            if (!TryGetKeyword(tokens, i, "AS"))
                break;
            i++;

            if (!TryGetPunctuation(tokens, i, "("))
                break;

            i = SkipParenthesized(tokens, i);

            if (TryGetPunctuation(tokens, i, ","))
            {
                i++;
                continue;
            }

            break;
        }
    }

    private static void ParseFromAndJoinBindings(
        IReadOnlyList<SqlToken> tokens,
        ISet<string> cteNames,
        ICollection<SqlTableBindingSymbol> bindings)
    {
        int subqueryCounter = 0;
        for (int i = 0; i < tokens.Count; i++)
        {
            if (!IsFromOrJoinKeyword(tokens[i]))
                continue;

            int j = i + 1;
            if (j >= tokens.Count)
                continue;

            if (TryGetPunctuation(tokens, j, "("))
            {
                int end = SkipParenthesized(tokens, j);
                j = end;
                if (TryGetKeyword(tokens, j, "AS"))
                    j++;

                if (!TryReadSingleIdentifier(tokens, ref j, DatabaseProvider.Postgres, out string? subAlias)
                    || string.IsNullOrWhiteSpace(subAlias))
                {
                    continue;
                }

                subqueryCounter++;
                bindings.Add(new SqlTableBindingSymbol(
                    TableRef: $"__subquery_{subqueryCounter}",
                    Alias: subAlias,
                    IsCte: false,
                    IsSubquery: true,
                    Order: i));
                continue;
            }

            if (!TryReadCompoundIdentifier(tokens, ref j, DatabaseProvider.Postgres, out string? tableRef)
                || string.IsNullOrWhiteSpace(tableRef))
            {
                continue;
            }

            string alias = BuildAlias(tableRef.Split('.').Last());
            int aliasCursor = j;
            if (TryGetKeyword(tokens, aliasCursor, "AS"))
                aliasCursor++;

            if (TryReadSingleIdentifier(tokens, ref aliasCursor, DatabaseProvider.Postgres, out string? explicitAlias)
                && !string.IsNullOrWhiteSpace(explicitAlias))
            {
                alias = explicitAlias;
            }

            bool isCte = cteNames.Contains(tableRef);
            bindings.Add(new SqlTableBindingSymbol(
                TableRef: tableRef,
                Alias: alias,
                IsCte: isCte,
                IsSubquery: false,
                Order: i));
        }
    }

    private static void ApplyProviderFallbackForQuotedSubqueryAliases(
        string source,
        DatabaseProvider provider,
        ISet<string> cteNames,
        ICollection<SqlTableBindingSymbol> bindings)
    {
        Regex? regex = provider switch
        {
            DatabaseProvider.SqlServer => new Regex(@"\)\s*(?:AS\s+)?\[([^\]]+)\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            DatabaseProvider.MySql => new Regex(@"\)\s*(?:AS\s+)?`([^`]+)`", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            _ => null,
        };

        if (regex is null)
            return;

        int order = bindings.Count == 0 ? 0 : bindings.Max(static b => b.Order) + 1;
        int subqueryCounter = bindings.Count(static b => b.IsSubquery);

        foreach (Match match in regex.Matches(source))
        {
            string alias = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(alias))
                continue;

            bool alreadyExists = bindings.Any(binding =>
                string.Equals(binding.Alias, alias, StringComparison.OrdinalIgnoreCase));
            if (alreadyExists)
                continue;

            subqueryCounter++;
            bindings.Add(new SqlTableBindingSymbol(
                TableRef: $"__subquery_{subqueryCounter}",
                Alias: alias,
                IsCte: cteNames.Contains(alias),
                IsSubquery: true,
                Order: order++));
        }
    }

    private static int SkipParenthesized(IReadOnlyList<SqlToken> tokens, int openParenIndex)
    {
        int depth = 0;
        for (int i = openParenIndex; i < tokens.Count; i++)
        {
            if (TryGetPunctuation(tokens, i, "("))
                depth++;
            else if (TryGetPunctuation(tokens, i, ")"))
                depth--;

            if (depth == 0)
                return i + 1;
        }

        return tokens.Count;
    }

    private static bool TryReadCompoundIdentifier(
        IReadOnlyList<SqlToken> tokens,
        ref int index,
        DatabaseProvider provider,
        out string? value)
    {
        value = null;
        int cursor = index;
        if (!TryReadSingleIdentifier(tokens, ref cursor, provider, out string? firstPart) || string.IsNullOrWhiteSpace(firstPart))
            return false;

        var parts = new List<string> { firstPart };
        while (TryGetPunctuation(tokens, cursor, "."))
        {
            int nextPartCursor = cursor + 1;
            if (!TryReadSingleIdentifier(tokens, ref nextPartCursor, provider, out string? nextPart)
                || string.IsNullOrWhiteSpace(nextPart))
            {
                break;
            }

            parts.Add(nextPart);
            cursor = nextPartCursor;
        }

        index = cursor;
        value = string.Join(".", parts);
        return true;
    }

    private static bool TryReadSingleIdentifier(
        IReadOnlyList<SqlToken> tokens,
        ref int index,
        DatabaseProvider provider,
        out string? value)
    {
        value = null;
        if (index < 0 || index >= tokens.Count)
            return false;

        SqlToken token = tokens[index];
        if (token.Kind == SqlTokenKind.Identifier)
        {
            value = token.Value;
            index++;
            return true;
        }

        if (token.Kind == SqlTokenKind.QuotedIdentifier)
        {
            value = UnquoteIdentifier(token.Value);
            index++;
            return true;
        }

        if (provider == DatabaseProvider.SqlServer
            && TryGetPunctuation(tokens, index, "[")
            && index + 2 < tokens.Count
            && tokens[index + 1].Kind == SqlTokenKind.Identifier
            && TryGetPunctuation(tokens, index + 2, "]"))
        {
            value = tokens[index + 1].Value;
            index += 3;
            return true;
        }

        return false;
    }

    private static bool IsFromOrJoinKeyword(SqlToken token)
    {
        if (token.Kind != SqlTokenKind.Keyword)
            return false;

        return token.Value.Equals("FROM", StringComparison.OrdinalIgnoreCase)
               || token.Value.Equals("JOIN", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetKeyword(IReadOnlyList<SqlToken> tokens, int index, string keyword)
    {
        if (index < 0 || index >= tokens.Count)
            return false;

        SqlToken token = tokens[index];
        return token.Kind == SqlTokenKind.Keyword
               && string.Equals(token.Value, keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetPunctuation(IReadOnlyList<SqlToken> tokens, int index, string punctuation)
    {
        if (index < 0 || index >= tokens.Count)
            return false;

        SqlToken token = tokens[index];
        return token.Kind == SqlTokenKind.Punctuation
               && string.Equals(token.Value, punctuation, StringComparison.Ordinal);
    }

    private static string UnquoteIdentifier(string quoted)
    {
        if (string.IsNullOrWhiteSpace(quoted))
            return string.Empty;

        if (quoted.Length >= 2 && quoted[0] == '"' && quoted[^1] == '"')
            return quoted[1..^1];

        return quoted;
    }

    private static string BuildAlias(string tableName)
    {
        string[] parts = tableName
            .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return "t";
        if (parts.Length == 1)
            return parts[0][0].ToString().ToLowerInvariant();

        return string.Concat(parts.Select(p => char.ToLowerInvariant(p[0])));
    }
}
