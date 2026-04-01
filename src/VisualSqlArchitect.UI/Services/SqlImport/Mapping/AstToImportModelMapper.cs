using System.Text.RegularExpressions;
using VisualSqlArchitect.UI.Services.SqlImport.Model;

namespace VisualSqlArchitect.UI.Services.SqlImport.Mapping;

public sealed class AstToImportModelMapper
{
    public SqlImportMappingResult Map(string sql)
    {
        string normalized = Normalize(sql);

        Match selMatch = Regex.Match(
            normalized,
            @"SELECT\s+(DISTINCT\s+)?(.+?)\s+FROM\b",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        if (!selMatch.Success)
            return new SqlImportMappingResult(null, [new("ParseError", "Could not parse SELECT/FROM.", "SELECT")]);

        bool isDistinct = selMatch.Groups[1].Success;
        string selectPart = selMatch.Groups[2].Value.Trim();

        Match fromMatch = Regex.Match(
            normalized,
            @"FROM\s+(.+?)(?=\s+(?:WHERE|GROUP\s+BY|HAVING|ORDER\s+BY|LIMIT|TOP)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        if (!fromMatch.Success)
            return new SqlImportMappingResult(null, [new("ParseError", "Could not parse FROM.", "FROM")]);

        string fromSql = fromMatch.Groups[1].Value.Trim();

        Match whereMatch = Regex.Match(
            normalized,
            @"WHERE\s+(.+?)(?=\s+(?:GROUP\s+BY|HAVING|ORDER\s+BY|LIMIT|TOP)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        Match groupMatch = Regex.Match(
            normalized,
            @"GROUP\s+BY\s+(.+?)(?=\s+(?:HAVING|ORDER\s+BY|LIMIT|TOP)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        Match havingMatch = Regex.Match(
            normalized,
            @"HAVING\s+(.+?)(?=\s+(?:ORDER\s+BY|LIMIT|TOP)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        Match orderMatch = Regex.Match(
            normalized,
            @"ORDER\s+BY\s+(.+?)(?=\s+(?:LIMIT|TOP)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );

        int? limit = null;
        int? top = null;
        Match limitMatch = Regex.Match(normalized, @"\bLIMIT\s+(\d+)", RegexOptions.IgnoreCase);
        if (limitMatch.Success)
            limit = int.Parse(limitMatch.Groups[1].Value);

        Match topMatch = Regex.Match(normalized, @"\bTOP\s+(\d+)", RegexOptions.IgnoreCase);
        if (topMatch.Success)
            top = int.Parse(topMatch.Groups[1].Value);

        ImportFrom primaryFrom = ParsePrimaryFrom(fromSql);
        IReadOnlyList<ImportJoin> joins = ParseJoins(fromSql);
        IReadOnlyList<ImportProjection> projections = ParseProjections(selectPart);
        IReadOnlyList<ImportExpression> groupBy = groupMatch.Success
            ? SplitCommas(groupMatch.Groups[1].Value).Select(t => new ImportExpression(t.Trim())).ToList()
            : [];
        IReadOnlyList<ImportOrderBy> orderBy = orderMatch.Success
            ? ParseOrderBy(orderMatch.Groups[1].Value)
            : [];

        var model = new SqlImportModel(
            new ImportSelect(isDistinct, projections),
            primaryFrom,
            joins,
            whereMatch.Success ? new ImportPredicate(whereMatch.Groups[1].Value.Trim()) : null,
            groupBy,
            havingMatch.Success ? new ImportPredicate(havingMatch.Groups[1].Value.Trim()) : null,
            orderBy,
            limit,
            top
        );

        List<SqlImportSemanticIssue> issues = ValidateAliasScope(model);
        return new SqlImportMappingResult(model, issues);
    }

    private static ImportFrom ParsePrimaryFrom(string fromSql)
    {
        Match m = Regex.Match(
            fromSql,
            @"^([A-Za-z_][A-Za-z0-9_\.]*)\s*(?:AS\s+([A-Za-z_][A-Za-z0-9_]*)|([A-Za-z_][A-Za-z0-9_]*))?",
            RegexOptions.IgnoreCase
        );
        if (!m.Success)
            return new ImportFrom(fromSql, null);

        string alias = m.Groups[2].Success ? m.Groups[2].Value : (m.Groups[3].Success ? m.Groups[3].Value : null) ?? string.Empty;
        return new ImportFrom(m.Groups[1].Value, string.IsNullOrWhiteSpace(alias) ? null : alias);
    }

    private static IReadOnlyList<ImportJoin> ParseJoins(string fromSql)
    {
        MatchCollection joinMatches = Regex.Matches(
            fromSql,
            @"(INNER\s+JOIN|LEFT\s+(?:OUTER\s+)?JOIN|RIGHT\s+(?:OUTER\s+)?JOIN|FULL\s+(?:OUTER\s+)?JOIN|JOIN)\s+([A-Za-z_][A-Za-z0-9_\.]*)\s*(?:AS\s+([A-Za-z_][A-Za-z0-9_]*)|([A-Za-z_][A-Za-z0-9_]*))?\s+ON\s+(.+?)(?=\s+(?:INNER\s+JOIN|LEFT\s+JOIN|RIGHT\s+JOIN|FULL\s+JOIN|JOIN)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );

        var joins = new List<ImportJoin>();
        foreach (Match jm in joinMatches)
        {
            string alias = jm.Groups[3].Success ? jm.Groups[3].Value : (jm.Groups[4].Success ? jm.Groups[4].Value : null) ?? string.Empty;
            joins.Add(
                new ImportJoin(
                    jm.Groups[1].Value.Trim().ToUpperInvariant(),
                    jm.Groups[2].Value.Trim(),
                    string.IsNullOrWhiteSpace(alias) ? null : alias,
                    new ImportPredicate(jm.Groups[5].Value.Trim())
                )
            );
        }

        return joins;
    }

    private static IReadOnlyList<ImportProjection> ParseProjections(string selectPart)
    {
        if (selectPart == "*")
            return [new(new ImportExpression("*"), null)];

        var projections = new List<ImportProjection>();
        foreach (string raw in SplitCommas(selectPart))
        {
            string part = raw.Trim();
            Match asMatch = Regex.Match(part, @"^(.+?)\s+AS\s+([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.IgnoreCase);
            if (asMatch.Success)
            {
                projections.Add(new ImportProjection(new ImportExpression(asMatch.Groups[1].Value.Trim()), asMatch.Groups[2].Value.Trim()));
                continue;
            }

            Match tailAlias = Regex.Match(part, @"^(.+?)\s+([A-Za-z_][A-Za-z0-9_]*)$");
            if (tailAlias.Success && !Regex.IsMatch(tailAlias.Groups[1].Value, @"\)$"))
            {
                projections.Add(new ImportProjection(new ImportExpression(tailAlias.Groups[1].Value.Trim()), tailAlias.Groups[2].Value.Trim()));
                continue;
            }

            projections.Add(new ImportProjection(new ImportExpression(part), null));
        }

        return projections;
    }

    private static IReadOnlyList<ImportOrderBy> ParseOrderBy(string orderByPart)
    {
        var terms = new List<ImportOrderBy>();
        foreach (string raw in SplitCommas(orderByPart))
        {
            Match tm = Regex.Match(raw.Trim(), @"^(.+?)(?:\s+(ASC|DESC))?$", RegexOptions.IgnoreCase);
            if (!tm.Success)
                continue;

            bool desc = tm.Groups[2].Success && tm.Groups[2].Value.Equals("DESC", StringComparison.OrdinalIgnoreCase);
            terms.Add(new ImportOrderBy(new ImportExpression(tm.Groups[1].Value.Trim()), desc));
        }

        return terms;
    }

    private static List<SqlImportSemanticIssue> ValidateAliasScope(SqlImportModel model)
    {
        var issues = new List<SqlImportSemanticIssue>();
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        RegisterAlias(model.From.Alias ?? model.From.Source.Split('.').Last(), aliases, issues, "FROM");
        foreach (ImportJoin join in model.Joins)
            RegisterAlias(join.Alias ?? join.Source.Split('.').Last(), aliases, issues, $"JOIN {join.Source}");

        foreach (string qualified in ExtractQualifiedPrefixes(model))
        {
            if (aliases.Contains(qualified))
                continue;

            issues.Add(
                new SqlImportSemanticIssue(
                    "UnknownAlias",
                    $"Alias '{qualified}' is not defined in FROM/JOIN scope.",
                    qualified
                )
            );
        }

        return issues;
    }

    private static IEnumerable<string> ExtractQualifiedPrefixes(SqlImportModel model)
    {
        IEnumerable<string> Scan(string text) => Regex
            .Matches(text, @"\b([A-Za-z_][A-Za-z0-9_]*)\.")
            .Select(m => m.Groups[1].Value);

        foreach (ImportProjection projection in model.Select.Projections)
            foreach (string prefix in Scan(projection.Expression.Text))
                yield return prefix;

        foreach (ImportJoin join in model.Joins)
            foreach (string prefix in Scan(join.On.Text))
                yield return prefix;

        if (model.Where is not null)
            foreach (string prefix in Scan(model.Where.Text))
                yield return prefix;

        foreach (ImportExpression term in model.GroupBy)
            foreach (string prefix in Scan(term.Text))
                yield return prefix;

        if (model.Having is not null)
            foreach (string prefix in Scan(model.Having.Text))
                yield return prefix;

        foreach (ImportOrderBy term in model.OrderBy)
            foreach (string prefix in Scan(term.Expression.Text))
                yield return prefix;
    }

    private static void RegisterAlias(
        string alias,
        HashSet<string> aliases,
        List<SqlImportSemanticIssue> issues,
        string context
    )
    {
        if (aliases.Add(alias))
            return;

        issues.Add(
            new SqlImportSemanticIssue(
                "DuplicateAlias",
                $"Alias '{alias}' is duplicated in source scope.",
                context
            )
        );
    }

    private static string Normalize(string sql)
    {
        string normalized = Regex.Replace(sql, @"--[^\n]*", " ");
        normalized = Regex.Replace(normalized, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized;
    }

    private static List<string> SplitCommas(string s)
    {
        var parts = new List<string>();
        int depth = 0;
        int start = 0;

        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '(')
                depth++;
            else if (s[i] == ')')
                depth--;
            else if (s[i] == ',' && depth == 0)
            {
                parts.Add(s[start..i]);
                start = i + 1;
            }
        }

        parts.Add(s[start..]);
        return parts;
    }
}
