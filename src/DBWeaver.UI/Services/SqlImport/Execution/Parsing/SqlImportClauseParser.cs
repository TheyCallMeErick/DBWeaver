using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using DBWeaver.UI.Services.SqlImport.Rewriting;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.SqlImport.Execution.Parsing;

public sealed class SqlImportClauseParser(SqlImportCteRewriteService cteRewriteService)
{
    private readonly SqlImportCteRewriteService _cteRewriteService = cteRewriteService;

    public SqlImportParseResult Parse(
        string sql,
        ObservableCollection<ImportReportItem> report,
        CancellationToken cancellationToken
    )
    {
        int imported = 0;
        int partial = 0;
        int skipped = 0;

        sql = Regex.Replace(sql, @"--[^\n]*", " ");
        sql = Regex.Replace(sql, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        sql = Regex.Replace(sql, @"\s+", " ").Trim();

        if (Regex.IsMatch(sql, @"\bWITH\b", RegexOptions.IgnoreCase)
            && _cteRewriteService.TryRewriteSimpleCteQuery(sql, out string rewrittenSql, out int rewrittenCteCount))
        {
            sql = rewrittenSql;
            report.Add(
                new ImportReportItem(
                    "CTE",
                    ImportItemStatus.Imported,
                    rewrittenCteCount == 1
                        ? "Single CTE rewritten to supported import shape."
                        : $"{rewrittenCteCount} chained CTEs rewritten to supported import shape."
                )
            );
            imported++;
        }

        string qualifiedIdentifierPattern = SqlImportIdentifierNormalizer.QualifiedIdentifierPattern;

        bool hasSupportedWhereSubquery = Regex.IsMatch(
            sql,
            $@"\bWHERE\s+\(*\s*(?:(?:NOT\s+)?EXISTS\s*\(\s*SELECT|{qualifiedIdentifierPattern}\s+(?:NOT\s+)?IN\s*\(\s*SELECT|{qualifiedIdentifierPattern}\s*(?:<>|!=|>=|<=|=|>|<)\s*\(+\s*SELECT)",
            RegexOptions.IgnoreCase
        );

        bool hasUnsupportedCteOrSubquery =
            Regex.IsMatch(sql, @"\bWITH\b", RegexOptions.IgnoreCase)
            || (Regex.IsMatch(sql, @"\(SELECT\b", RegexOptions.IgnoreCase) && !hasSupportedWhereSubquery);

        bool hasCorrelatedSubquery =
            Regex.IsMatch(sql, @"\b(EXISTS|IN)\s*\(\s*SELECT\b", RegexOptions.IgnoreCase)
            && Regex.IsMatch(sql, @"\b\w+\.\w+\s*=\s*\w+\.\w+\b", RegexOptions.IgnoreCase);

        Match fromMatchForAlias = Regex.Match(
            sql,
            @"FROM\s+(.+?)(?=\s+(?:WHERE|ORDER\s+BY|LIMIT|GROUP\s+BY)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );

        HashSet<string> outerAliases = _cteRewriteService.ExtractSourceAliases(
            fromMatchForAlias.Success ? fromMatchForAlias.Groups[1].Value.Trim() : string.Empty
        );

        if (hasUnsupportedCteOrSubquery)
        {
            if (Regex.IsMatch(sql, @"\bWITH\b", RegexOptions.IgnoreCase))
            {
                foreach (string cteIssue in _cteRewriteService.AnalyzeCteNameIssues(sql))
                {
                    report.Add(new ImportReportItem("CTE name diagnostics", ImportItemStatus.Partial, cteIssue));
                    partial++;
                }
            }

            if (hasCorrelatedSubquery)
            {
                string correlatedFields = SqlImportCteRewriteService.DescribeCorrelatedOuterReferences(sql, outerAliases);
                report.Add(
                    new ImportReportItem(
                        "Correlated sub-query",
                        ImportItemStatus.Partial,
                        string.IsNullOrWhiteSpace(correlatedFields)
                            ? "Correlated sub-query is not yet supported and falls back to a safe partial import path."
                            : $"Correlated sub-query is not yet supported and falls back to a safe partial import path. External refs: {correlatedFields}."
                    )
                );
                partial++;
            }

            report.Add(new ImportReportItem("CTE / sub-query", ImportItemStatus.Skipped, "CTEs and sub-queries are not supported"));
            skipped++;

            report.Add(
                new ImportReportItem(
                    "Raw fallback",
                    ImportItemStatus.Skipped,
                    "Raw fallback is disabled for CTE/sub-query blocks to avoid unsafe or ambiguous SQL materialization."
                )
            );
            skipped++;

            return new SqlImportParseResult(null, imported, partial, skipped, true);
        }

        if (Regex.IsMatch(sql, @"\bUNION\b", RegexOptions.IgnoreCase))
        {
            report.Add(new ImportReportItem("UNION", ImportItemStatus.Skipped, "UNION is not supported"));
            skipped++;
        }

        int? topLimit = null;
        Match distinctTopPrefix = Regex.Match(
            sql,
            @"^\s*SELECT\s+DISTINCT\s+TOP\s+(?<top>\d+)\s+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (distinctTopPrefix.Success)
        {
            topLimit = int.Parse(distinctTopPrefix.Groups["top"].Value);
            sql = Regex.Replace(
                sql,
                @"^\s*SELECT\s+DISTINCT\s+TOP\s+\d+\s+",
                "SELECT DISTINCT ",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        else
        {
            Match topPrefix = Regex.Match(
                sql,
                @"^\s*SELECT\s+TOP\s+(?<top>\d+)\s+",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (topPrefix.Success)
            {
                topLimit = int.Parse(topPrefix.Groups["top"].Value);
                sql = Regex.Replace(
                    sql,
                    @"^\s*SELECT\s+TOP\s+\d+\s+",
                    "SELECT ",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
        }

        Match selMatch = Regex.Match(
            sql,
            @"SELECT\s+(DISTINCT\s+)?(.+?)\s+FROM\b",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        if (!selMatch.Success)
            throw new InvalidOperationException("Could not find SELECT ... FROM in the query.");

        bool isDistinct = selMatch.Groups[1].Success;
        string colPart = selMatch.Groups[2].Value.Trim();
        Match qualifiedStarMatch = Regex.Match(
            colPart,
            $@"^\s*(?<source>{SqlImportIdentifierNormalizer.QualifiedIdentifierPattern})\s*\.\s*\*\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        bool isStar = colPart == "*" || qualifiedStarMatch.Success;
        string? starQualifier = qualifiedStarMatch.Success
            ? SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(qualifiedStarMatch.Groups["source"].Value)
            : null;

        var selectedCols = new List<SqlImportSelectedColumn>();
        if (!isStar)
        {
            foreach (string raw in SplitCommas(colPart))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string col = raw.Trim();
                Match asMatch = Regex.Match(
                    col,
                    $@"^(.+?)\s+AS\s+({SqlImportIdentifierNormalizer.IdentifierPattern})$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (asMatch.Success)
                {
                    selectedCols.Add(new SqlImportSelectedColumn(
                        asMatch.Groups[1].Value.Trim(),
                        SqlImportIdentifierNormalizer.NormalizeIdentifierToken(asMatch.Groups[2].Value)));
                }
                else
                    selectedCols.Add(new SqlImportSelectedColumn(col, null));
            }
        }

        Match fromBlock = Regex.Match(
            sql,
            @"FROM\s+(.+?)(?=\s+(?:WHERE|ORDER\s+BY|LIMIT|OFFSET|GROUP\s+BY|HAVING)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        if (!fromBlock.Success)
            throw new InvalidOperationException("Could not parse FROM clause.");

        string fromSql = fromBlock.Groups[1].Value.Trim();

        string[] joinKeywords = ["INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN", "JOIN"];
        var fromParts = new List<SqlImportSourcePart>();

        int firstJoinIdx = -1;
        string upperFrom = fromSql.ToUpperInvariant();
        foreach (string joinKeyword in joinKeywords)
        {
            int idx = upperFrom.IndexOf(joinKeyword, StringComparison.Ordinal);
            if (idx >= 0 && (firstJoinIdx < 0 || idx < firstJoinIdx))
                firstJoinIdx = idx;
        }

        string primaryPart = firstJoinIdx >= 0 ? fromSql[..firstJoinIdx].Trim() : fromSql.Trim();
        (string primaryTable, string? primaryAlias) = ExtractTableAndAlias(primaryPart);
        fromParts.Add(new SqlImportSourcePart(primaryTable, primaryAlias, null, null));

        MatchCollection joinMatches = Regex.Matches(
            fromSql,
            $@"(?:INNER\s+JOIN|LEFT\s+(?:OUTER\s+)?JOIN|RIGHT\s+(?:OUTER\s+)?JOIN|FULL\s+(?:OUTER\s+)?JOIN|JOIN)\s+({qualifiedIdentifierPattern})(?:\s+(?:AS\s+)?({SqlImportIdentifierNormalizer.IdentifierPattern}))?\s+ON\s+(.+?)(?=\s+(?:INNER\s+JOIN|LEFT\s+(?:OUTER\s+)?JOIN|RIGHT\s+(?:OUTER\s+)?JOIN|FULL\s+(?:OUTER\s+)?JOIN|JOIN)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant
        );
        foreach (Match jm in joinMatches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string jTable = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(jm.Groups[1].Value);
            string? jAlias = jm.Groups[2].Success
                ? SqlImportIdentifierNormalizer.NormalizeIdentifierToken(jm.Groups[2].Value)
                : null;
            string onClause = jm.Groups[3].Value.Trim();
            string jType = Regex.Match(
                    jm.Value,
                    @"(?:INNER\s+JOIN|LEFT\s+(?:OUTER\s+)?JOIN|RIGHT\s+(?:OUTER\s+)?JOIN|FULL\s+(?:OUTER\s+)?JOIN|JOIN)",
                    RegexOptions.IgnoreCase
                )
                .Value.Trim()
                .ToUpperInvariant();
            fromParts.Add(new SqlImportSourcePart(jTable, jAlias, jType, onClause));
        }

        Match whereMatch = Regex.Match(
            sql,
            @"WHERE\s+(.+?)(?=\s+(?:GROUP\s+BY|HAVING|ORDER\s+BY|LIMIT|OFFSET|TOP)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        string? whereClause = whereMatch.Success ? whereMatch.Groups[1].Value.Trim() : null;

        Match orderMatch = Regex.Match(
            sql,
            @"ORDER\s+BY\s+(.+?)(?=\s+(?:LIMIT|OFFSET)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        string? orderBy = orderMatch.Success ? orderMatch.Groups[1].Value.Trim() : null;

        Match groupMatch = Regex.Match(
            sql,
            @"GROUP\s+BY\s+(.+?)(?=\s+(?:HAVING|ORDER\s+BY|LIMIT|TOP)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        string? groupBy = groupMatch.Success ? groupMatch.Groups[1].Value.Trim() : null;

        Match havingMatch = Regex.Match(
            sql,
            @"HAVING\s+(.+?)(?=\s+(?:ORDER\s+BY|LIMIT|TOP)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        string? havingClause = havingMatch.Success ? havingMatch.Groups[1].Value.Trim() : null;

        int? limitVal = null;
        Match limitMatch = Regex.Match(sql, @"\bLIMIT\s+(\d+)", RegexOptions.IgnoreCase);
        if (limitMatch.Success)
            limitVal = int.Parse(limitMatch.Groups[1].Value);
        else
        {
            Match topMatch = Regex.Match(sql, @"\bTOP\s+(\d+)", RegexOptions.IgnoreCase);
            if (topMatch.Success)
                limitVal = int.Parse(topMatch.Groups[1].Value);
            else if (topLimit.HasValue)
                limitVal = topLimit.Value;
        }

        var parsed = new SqlImportParsedQuery(
            isDistinct,
            isStar,
            starQualifier,
            selectedCols,
            fromParts,
            whereClause,
            orderBy,
            groupBy,
            havingClause,
            limitVal,
            outerAliases
        );

        return new SqlImportParseResult(parsed, imported, partial, skipped, false);
    }

    private static (string Table, string? Alias) ExtractTableAndAlias(string part)
    {
        string trimmed = part.Trim();
        Match match = Regex.Match(
            trimmed,
            $@"^({SqlImportIdentifierNormalizer.QualifiedIdentifierPattern})(?:\s+(?:AS\s+)?({SqlImportIdentifierNormalizer.IdentifierPattern}))?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (match.Success)
        {
            string table = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(match.Groups[1].Value);
            string? alias = match.Groups[2].Success
                ? SqlImportIdentifierNormalizer.NormalizeIdentifierToken(match.Groups[2].Value)
                : null;
            return (table, alias);
        }

        string firstToken = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? trimmed;
        return (SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(firstToken), null);
    }

    private static List<string> SplitCommas(string value)
    {
        var parts = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '(')
                depth++;
            else if (value[i] == ')')
                depth--;
            else if (value[i] == ',' && depth == 0)
            {
                parts.Add(value[start..i]);
                start = i + 1;
            }
        }

        parts.Add(value[start..]);
        return parts;
    }
}
