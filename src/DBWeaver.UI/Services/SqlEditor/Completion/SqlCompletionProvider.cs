using DBWeaver.Core;
using DBWeaver.Metadata;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlCompletionProvider
{
    private static readonly SqlTokenizer Tokenizer = new();
    private static readonly SqlStatementExtractor StatementExtractor = new();
    private static readonly SqlContextDetector ContextDetector = new();
    private static readonly SqlSymbolTableBuilder SymbolTableBuilder = new();
    private readonly CompletionRankingEngine _rankingEngine;
    private readonly CompletionUsageStats _usageStats;

    public SqlCompletionProvider(
        CompletionRankingEngine? rankingEngine = null,
        CompletionUsageStats? usageStats = null)
    {
        _rankingEngine = rankingEngine ?? new CompletionRankingEngine();
        _usageStats = usageStats ?? new CompletionUsageStats();
    }

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
        DatabaseProvider? provider = null,
        string? connectionProfileId = null)
    {
        ArgumentNullException.ThrowIfNull(fullText);
        if (caretOffset < 0 || caretOffset > fullText.Length)
            throw new ArgumentOutOfRangeException(nameof(caretOffset));

        DatabaseProvider resolvedProvider = provider ?? metadata?.Provider ?? DatabaseProvider.Postgres;
        int prefixStart = FindPrefixStart(fullText, caretOffset);
        string prefix = fullText[prefixStart..caretOffset];
        string beforeCaret = fullText[..caretOffset];
        IReadOnlyList<SqlToken> allTokens = Tokenizer.Tokenize(fullText);
        SqlStatementContext statementContext = StatementExtractor.Extract(allTokens, caretOffset);
        SqlCompletionContext completionContext = ContextDetector.Detect(statementContext.Tokens, caretOffset);

        int statementBeforeCaretStart = Math.Min(statementContext.StartOffset, caretOffset);
        int statementBeforeCaretLength = Math.Max(0, caretOffset - statementBeforeCaretStart);
        string statementBeforeCaret = statementBeforeCaretLength > 0
            ? fullText.Substring(statementBeforeCaretStart, statementBeforeCaretLength)
            : string.Empty;
        SqlSymbolTable symbolTable = SymbolTableBuilder.Build(statementBeforeCaret, resolvedProvider);

        var suggestions = new List<SqlCompletionSuggestion>();
        suggestions.AddRange(SuggestKeywords());
        suggestions.AddRange(SuggestFunctions(resolvedProvider));
        suggestions.AddRange(SuggestSnippets());
        if (IsTableContext(completionContext))
            suggestions.AddRange(SuggestCtes(symbolTable));

        if (metadata is not null)
        {
            if (IsTableContext(completionContext))
                suggestions.AddRange(SuggestTables(metadata));

            if (IsJoinContext(completionContext))
                suggestions.AddRange(SuggestSmartJoins(metadata, symbolTable));

            string? qualifier = TryGetQualifier(beforeCaret, prefixStart);
            if (!string.IsNullOrWhiteSpace(qualifier))
                suggestions.AddRange(SuggestColumnsForQualifier(metadata, symbolTable, qualifier));
            else if (IsColumnContext(completionContext))
                suggestions.AddRange(SuggestColumnsInScope(metadata, symbolTable));
        }

        IReadOnlyList<SqlCompletionSuggestion> ranked = _rankingEngine.Rank(
            suggestions,
            prefix,
            symbolTable,
            _usageStats,
            connectionProfileId);

        IEnumerable<SqlCompletionSuggestion> filtered = ranked
            .GroupBy(s => s.Label, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        return new SqlCompletionRequest
        {
            PrefixLength = prefix.Length,
            Suggestions = filtered.ToList(),
        };
    }

    public void RecordAcceptedSuggestion(string? suggestionLabel, string? connectionProfileId)
    {
        if (string.IsNullOrWhiteSpace(suggestionLabel))
            return;

        _usageStats.RecordAccepted(suggestionLabel, connectionProfileId);
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

    private static IEnumerable<SqlCompletionSuggestion> SuggestCtes(SqlSymbolTable symbolTable)
    {
        foreach (string cteName in symbolTable.CteNames.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase))
        {
            string alias = BuildAlias(cteName.Split('.').Last());
            yield return new SqlCompletionSuggestion(
                cteName,
                cteName,
                "CTE",
                SqlCompletionKind.Table);
            yield return new SqlCompletionSuggestion(
                $"{cteName} AS {alias}",
                $"{cteName} AS {alias}",
                "CTE with alias",
                SqlCompletionKind.Table);
        }
    }

    private static IEnumerable<SqlCompletionSuggestion> SuggestColumnsForQualifier(
        DbMetadata metadata,
        SqlSymbolTable symbolTable,
        string qualifier)
    {
        string tableRef = qualifier;
        if (symbolTable.TryResolveBinding(qualifier, out SqlTableBindingSymbol? binding)
            && binding is not null
            && !binding.IsSubquery)
        {
            tableRef = binding.TableRef;
        }

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

    private static IEnumerable<SqlCompletionSuggestion> SuggestColumnsInScope(DbMetadata metadata, SqlSymbolTable symbolTable)
    {
        if (symbolTable.BindingsInOrder.Count == 0)
            yield break;

        foreach (SqlTableBindingSymbol binding in symbolTable.BindingsInOrder)
        {
            if (binding.IsSubquery)
                continue;

            TableMetadata? table = ResolveTable(metadata, binding.TableRef);
            if (table is null)
                continue;

            foreach (ColumnMetadata col in table.Columns.OrderBy(c => c.OrdinalPosition))
            {
                string label = $"{binding.Alias}.{col.Name}";
                yield return new SqlCompletionSuggestion(
                    label,
                    label,
                    BuildColumnDetail(table, col),
                    SqlCompletionKind.Column);
            }
        }
    }

    private static IEnumerable<SqlCompletionSuggestion> SuggestSmartJoins(DbMetadata metadata, SqlSymbolTable symbolTable)
    {
        SqlTableBindingSymbol? anchor = symbolTable.BindingsInOrder.LastOrDefault(static binding => !binding.IsSubquery);
        if (anchor is null)
            yield break;

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

    private static bool IsTableContext(SqlCompletionContext context)
    {
        return context is SqlCompletionContext.FromClause
            or SqlCompletionContext.JoinClause
            or SqlCompletionContext.InsertColumns;
    }

    private static bool IsJoinContext(SqlCompletionContext context)
    {
        return context == SqlCompletionContext.JoinClause;
    }

    private static bool IsColumnContext(SqlCompletionContext context)
    {
        return context is SqlCompletionContext.SelectList
            or SqlCompletionContext.WhereClause
            or SqlCompletionContext.OnClause
            or SqlCompletionContext.OrderByClause
            or SqlCompletionContext.GroupByClause
            or SqlCompletionContext.HavingClause
            or SqlCompletionContext.UpdateSetClause;
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

}
