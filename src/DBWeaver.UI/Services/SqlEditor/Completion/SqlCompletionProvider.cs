using System.Text.RegularExpressions;
using DBWeaver.Core;
using DBWeaver.Metadata;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlCompletionProvider
{
    private static readonly string[] Keywords =
    [
        "SELECT", "FROM", "WHERE", "JOIN", "LEFT", "RIGHT", "INNER", "OUTER",
        "ON", "GROUP", "BY", "ORDER", "HAVING", "LIMIT", "OFFSET", "INSERT",
        "INTO", "VALUES", "UPDATE", "SET", "DELETE", "CREATE", "ALTER", "DROP",
        "AND", "OR", "NOT", "NULL", "AS", "DISTINCT",
    ];

    public SqlCompletionRequest GetSuggestions(
        string fullText,
        int caretOffset,
        DbMetadata? metadata,
        DatabaseProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(fullText);
        if (caretOffset < 0 || caretOffset > fullText.Length)
            throw new ArgumentOutOfRangeException(nameof(caretOffset));

        DatabaseProvider resolvedProvider = provider ?? metadata?.Provider ?? DatabaseProvider.Postgres;
        int prefixStart = FindPrefixStart(fullText, caretOffset);
        string prefix = fullText[prefixStart..caretOffset];
        string beforeCaret = fullText[..caretOffset];
        string statementBeforeCaret = ExtractCurrentStatement(beforeCaret);

        var suggestions = new List<SqlCompletionSuggestion>();
        suggestions.AddRange(SuggestKeywords());
        suggestions.AddRange(SuggestFunctions(resolvedProvider));
        suggestions.AddRange(SuggestSnippets());

        if (metadata is not null)
        {
            if (IsTableContext(statementBeforeCaret))
                suggestions.AddRange(SuggestTables(metadata));

            if (IsJoinContext(statementBeforeCaret))
                suggestions.AddRange(SuggestSmartJoins(statementBeforeCaret, metadata));

            string? qualifier = TryGetQualifier(beforeCaret, prefixStart);
            if (!string.IsNullOrWhiteSpace(qualifier))
                suggestions.AddRange(SuggestColumnsForQualifier(statementBeforeCaret, metadata, qualifier));
            else if (IsColumnContext(statementBeforeCaret))
                suggestions.AddRange(SuggestColumnsInScope(statementBeforeCaret, metadata));
        }

        IEnumerable<SqlCompletionSuggestion> filtered = suggestions
            .Where(s => string.IsNullOrWhiteSpace(prefix)
                || s.Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || s.Label.Split('.').Last().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .GroupBy(s => s.Label, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(s => s.Kind)
            .ThenBy(s => s.Label, StringComparer.OrdinalIgnoreCase);

        return new SqlCompletionRequest
        {
            PrefixLength = prefix.Length,
            Suggestions = filtered.ToList(),
        };
    }

    private static IEnumerable<SqlCompletionSuggestion> SuggestKeywords() =>
        Keywords.Select(k => new SqlCompletionSuggestion(k, k, "SQL keyword", SqlCompletionKind.Keyword));

    private static IReadOnlyList<SqlCompletionSuggestion> SuggestFunctions(DatabaseProvider provider) =>
        provider switch
        {
            DatabaseProvider.Postgres =>
            [
                CreateFunction("NOW()", "Current timestamp"),
                CreateFunction("DATE_TRUNC('day', )", "Date truncate"),
                CreateFunction("COALESCE(, )", "First non-null value"),
                CreateFunction("STRING_AGG(, ',')", "String aggregation"),
                CreateFunction("JSONB_EXTRACT_PATH_TEXT(, )", "JSONB path extraction"),
            ],
            DatabaseProvider.MySql =>
            [
                CreateFunction("NOW()", "Current timestamp"),
                CreateFunction("DATE_FORMAT(, '%Y-%m-%d')", "Date formatting"),
                CreateFunction("IFNULL(, )", "Null fallback"),
                CreateFunction("GROUP_CONCAT( SEPARATOR ',')", "String aggregation"),
                CreateFunction("JSON_EXTRACT(, '$.path')", "JSON path extraction"),
            ],
            DatabaseProvider.SqlServer =>
            [
                CreateFunction("GETDATE()", "Current timestamp"),
                CreateFunction("DATEADD(day, 1, )", "Date arithmetic"),
                CreateFunction("ISNULL(, )", "Null fallback"),
                CreateFunction("STRING_AGG(, ',')", "String aggregation"),
                CreateFunction("JSON_VALUE(, '$.path')", "JSON path extraction"),
            ],
            DatabaseProvider.SQLite =>
            [
                CreateFunction("datetime('now')", "Current timestamp"),
                CreateFunction("strftime('%Y-%m-%d', )", "Date formatting"),
                CreateFunction("ifnull(, )", "Null fallback"),
                CreateFunction("group_concat(, ',')", "String aggregation"),
                CreateFunction("json_extract(, '$.path')", "JSON path extraction"),
            ],
            _ => [],
        };

    private static SqlCompletionSuggestion CreateFunction(string label, string detail) =>
        new(label, label, detail, SqlCompletionKind.Function);

    private static IReadOnlyList<SqlCompletionSuggestion> SuggestSnippets() =>
    [
        new(
            "SELECT ... FROM ...",
            "SELECT\n  \nFROM ",
            "Basic query skeleton",
            SqlCompletionKind.Snippet),
        new(
            "SELECT ... FROM ... WHERE ...",
            "SELECT\n  \nFROM \nWHERE ",
            "Query skeleton with filter",
            SqlCompletionKind.Snippet),
        new(
            "INSERT INTO ... VALUES ...",
            "INSERT INTO \n(\n  \n)\nVALUES\n(\n  \n);",
            "Insert statement skeleton",
            SqlCompletionKind.Snippet),
        new(
            "UPDATE ... SET ... WHERE ...",
            "UPDATE \nSET \nWHERE ;",
            "Update statement skeleton",
            SqlCompletionKind.Snippet),
    ];

    private static IEnumerable<SqlCompletionSuggestion> SuggestTables(DbMetadata metadata)
    {
        foreach (TableMetadata table in metadata.AllTables)
        {
            string alias = BuildAlias(table.Name);
            yield return new SqlCompletionSuggestion(
                table.FullName,
                table.FullName,
                $"Table ({table.Kind})",
                SqlCompletionKind.Table);
            yield return new SqlCompletionSuggestion(
                $"{table.FullName} AS {alias}",
                $"{table.FullName} AS {alias}",
                $"Table ({table.Kind}) with alias",
                SqlCompletionKind.Table);
        }
    }

    private static IEnumerable<SqlCompletionSuggestion> SuggestColumnsForQualifier(
        string statementBeforeCaret,
        DbMetadata metadata,
        string qualifier)
    {
        Dictionary<string, string> aliasMap = ExtractAliasMap(statementBeforeCaret);
        if (!aliasMap.TryGetValue(qualifier, out string? tableRef))
            tableRef = qualifier;

        TableMetadata? table = ResolveTable(metadata, tableRef);
        if (table is null)
            yield break;

        foreach (ColumnMetadata col in table.Columns.OrderBy(c => c.OrdinalPosition))
        {
            yield return new SqlCompletionSuggestion(
                col.Name,
                col.Name,
                BuildColumnDetail(table, col),
                SqlCompletionKind.Column);
        }
    }

    private static IEnumerable<SqlCompletionSuggestion> SuggestColumnsInScope(string statementBeforeCaret, DbMetadata metadata)
    {
        Dictionary<string, string> aliasMap = ExtractAliasMap(statementBeforeCaret);
        if (aliasMap.Count == 0)
            yield break;

        foreach ((string alias, string tableRef) in aliasMap)
        {
            TableMetadata? table = ResolveTable(metadata, tableRef);
            if (table is null)
                continue;

            foreach (ColumnMetadata col in table.Columns.OrderBy(c => c.OrdinalPosition))
            {
                string label = $"{alias}.{col.Name}";
                yield return new SqlCompletionSuggestion(
                    label,
                    label,
                    BuildColumnDetail(table, col),
                    SqlCompletionKind.Column);
            }
        }
    }

    private static IEnumerable<SqlCompletionSuggestion> SuggestSmartJoins(string statementBeforeCaret, DbMetadata metadata)
    {
        List<TableBinding> bindings = ExtractTableBindings(statementBeforeCaret);
        if (bindings.Count == 0)
            yield break;

        TableBinding anchor = bindings[^1];
        TableMetadata? anchorTable = ResolveTable(metadata, anchor.TableRef);
        if (anchorTable is null)
            yield break;

        var map = new Dictionary<string, SqlCompletionSuggestion>(StringComparer.OrdinalIgnoreCase);
        foreach (ForeignKeyRelation fk in metadata.AllForeignKeys)
        {
            if (fk.ChildFullTable.Equals(anchorTable.FullName, StringComparison.OrdinalIgnoreCase))
            {
                string targetTable = fk.ParentFullTable;
                string targetAlias = BuildAlias(targetTable.Split('.').Last());
                string insert = $"{targetTable} {targetAlias} ON {targetAlias}.{fk.ParentColumn} = {anchor.Alias}.{fk.ChildColumn}";
                string label = $"JOIN {insert}";
                map[label] = new SqlCompletionSuggestion(label, insert, "Smart join via foreign key", SqlCompletionKind.Join);
            }
            else if (fk.ParentFullTable.Equals(anchorTable.FullName, StringComparison.OrdinalIgnoreCase))
            {
                string targetTable = fk.ChildFullTable;
                string targetAlias = BuildAlias(targetTable.Split('.').Last());
                string insert = $"{targetTable} {targetAlias} ON {targetAlias}.{fk.ChildColumn} = {anchor.Alias}.{fk.ParentColumn}";
                string label = $"JOIN {insert}";
                map[label] = new SqlCompletionSuggestion(label, insert, "Smart join via foreign key", SqlCompletionKind.Join);
            }
        }

        foreach (SqlCompletionSuggestion suggestion in map.Values)
            yield return suggestion;
    }

    private static string BuildColumnDetail(TableMetadata table, ColumnMetadata column)
    {
        var flags = new List<string>();
        if (column.IsPrimaryKey)
            flags.Add("PK");
        if (column.IsForeignKey)
            flags.Add("FK");
        if (!column.IsNullable)
            flags.Add("NOT NULL");

        string suffix = flags.Count == 0 ? string.Empty : $" [{string.Join(", ", flags)}]";
        return $"{table.FullName}.{column.Name} ({column.DataType}){suffix}";
    }

    private static Dictionary<string, string> ExtractAliasMap(string statement)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (TableBinding binding in ExtractTableBindings(statement))
        {
            map[binding.Alias] = binding.TableRef;
            string shortName = binding.TableRef.Split('.').Last();
            if (!map.ContainsKey(shortName))
                map[shortName] = binding.TableRef;
        }

        return map;
    }

    private static List<TableBinding> ExtractTableBindings(string statement)
    {
        var bindings = new List<TableBinding>();
        foreach (Match match in Regex.Matches(
                     statement,
                     @"\b(?:FROM|JOIN|LEFT\s+JOIN|RIGHT\s+JOIN|INNER\s+JOIN|FULL\s+JOIN|CROSS\s+JOIN)\s+([A-Za-z_][A-Za-z0-9_\.]*)\s*(?:AS\s+)?([A-Za-z_][A-Za-z0-9_]*)?",
                     RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            string tableRef = match.Groups[1].Value;
            string alias = match.Groups[2].Success ? match.Groups[2].Value : BuildAlias(tableRef.Split('.').Last());
            bindings.Add(new TableBinding(tableRef, alias));
        }

        return bindings;
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

    private static TableMetadata? ResolveTable(DbMetadata metadata, string tableRef)
    {
        string normalized = tableRef.Trim();
        return metadata.AllTables.FirstOrDefault(t =>
                   t.FullName.Equals(normalized, StringComparison.OrdinalIgnoreCase))
               ?? metadata.AllTables.FirstOrDefault(t =>
                   t.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTableContext(string statementBeforeCaret)
    {
        string t = statementBeforeCaret.TrimEnd();
        return Regex.IsMatch(t, @"\b(FROM|JOIN|LEFT\s+JOIN|RIGHT\s+JOIN|INNER\s+JOIN|FULL\s+JOIN|CROSS\s+JOIN|UPDATE|INTO)\s+[A-Za-z0-9_\.]*$", RegexOptions.IgnoreCase)
               || Regex.IsMatch(t, @"\bDELETE\s+FROM\s+[A-Za-z0-9_\.]*$", RegexOptions.IgnoreCase);
    }

    private static bool IsJoinContext(string statementBeforeCaret)
    {
        string t = statementBeforeCaret.TrimEnd();
        return Regex.IsMatch(t, @"\b(JOIN|LEFT\s+JOIN|RIGHT\s+JOIN|INNER\s+JOIN|FULL\s+JOIN|CROSS\s+JOIN)\s*(?:[A-Za-z0-9_\.]*)$", RegexOptions.IgnoreCase);
    }

    private static bool IsColumnContext(string statementBeforeCaret)
    {
        string t = statementBeforeCaret.TrimEnd();
        return Regex.IsMatch(t, @"\b(SELECT|WHERE|ON|ORDER\s+BY|GROUP\s+BY)\s+[A-Za-z0-9_\.]*$", RegexOptions.IgnoreCase);
    }

    private static string ExtractCurrentStatement(string text)
    {
        int idx = text.LastIndexOf(';');
        return idx >= 0 ? text[(idx + 1)..] : text;
    }

    private static string? TryGetQualifier(string beforeCaret, int prefixStart)
    {
        int dotIndex = prefixStart - 1;
        if (dotIndex < 0 || beforeCaret[dotIndex] != '.')
            return null;

        int start = dotIndex - 1;
        while (start >= 0 && (char.IsLetterOrDigit(beforeCaret[start]) || beforeCaret[start] == '_'))
            start--;

        int tokenStart = start + 1;
        if (tokenStart >= dotIndex)
            return null;

        return beforeCaret[tokenStart..dotIndex];
    }

    private static int FindPrefixStart(string fullText, int caretOffset)
    {
        int start = caretOffset;
        while (start > 0 && (char.IsLetterOrDigit(fullText[start - 1]) || fullText[start - 1] == '_'))
            start--;

        return start;
    }

    private sealed record TableBinding(string TableRef, string Alias);
}
