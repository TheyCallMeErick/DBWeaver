using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using DBWeaver.Nodes;
using DBWeaver.UI.Services.SqlImport;
using DBWeaver.UI.Services.SqlImport.Build;
using DBWeaver.UI.Services.SqlImport.Execution.Parsing;
using DBWeaver.UI.Services.SqlImport.Rewriting;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.SqlImport.Execution.Applying;

internal sealed class SqlImportWhereClauseApplier(CanvasViewModel canvas) : ISqlImportApplyStep
{
    private readonly CanvasViewModel _canvas = canvas;

    public SqlImportApplyResult Apply(SqlImportApplyContext context)
    {
        SqlImportParsedQuery query = context.Query;
        ImportBuildContext coreContext = context.CoreContext;
        ObservableCollection<ImportReportItem> report = context.Report;
        var layout = new SqlImportLayoutCalculator(coreContext.Layout);

        if (query.WhereClause is null)
            return new SqlImportApplyResult(0, 0, 0);

        int imported = 0;
        int partial = 0;

        var tableNodes = coreContext.TableNodes;
        NodeViewModel result = coreContext.ResultNode;
        int sourceCount = query.FromParts.Count;
        string qualifiedIdentifierPattern = SqlImportIdentifierNormalizer.QualifiedIdentifierPattern;
        string whereClause = StripOuterParentheses(query.WhereClause);

        Match existsMatch = Regex.Match(
            whereClause,
            @"^\s*(?<not>NOT\s+)?EXISTS\s*\(\s*(?<subquery>SELECT.+)\s*\)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        if (existsMatch.Success)
        {
            bool negateExists = existsMatch.Groups["not"].Success;
            string subquerySql = existsMatch.Groups["subquery"].Value.Trim();
            string correlatedFields = SqlImportCteRewriteService.DescribeCorrelatedOuterReferences(subquerySql, query.OuterAliases);
            NodeViewModel existsNode = new(
                NodeDefinitionRegistry.Get(NodeType.SubqueryExists),
                layout.SubqueryPosition(sourceCount)
            );
            existsNode.Parameters["query"] = subquerySql;
            existsNode.Parameters["negate"] = negateExists ? "true" : "false";
            _canvas.Nodes.Add(existsNode);
            SqlImportClauseApplyUtilities.SafeWire(existsNode, "result", result, "where", _canvas);

            report.Add(new ImportReportItem(
                negateExists ? "WHERE NOT EXISTS(sub-query)" : "WHERE EXISTS(sub-query)",
                ImportItemStatus.Imported,
                sourceNodeId: existsNode.Id));
            imported++;

            if (!string.IsNullOrWhiteSpace(correlatedFields))
            {
                report.Add(
                    new ImportReportItem(
                        "Correlation fields",
                        ImportItemStatus.Imported,
                        $"External references: {correlatedFields}",
                        existsNode.Id
                    )
                );
                imported++;
            }

            return new SqlImportApplyResult(imported, partial, 0);
        }

        Match inSubqueryMatch = Regex.Match(
            whereClause,
            $@"^\s*(?<left>{qualifiedIdentifierPattern})\s+(?<not>NOT\s+)?IN\s*\((?<subquery>SELECT.+?)\)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant
        );
        if (inSubqueryMatch.Success)
        {
            string leftExpr = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(inSubqueryMatch.Groups["left"].Value)
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault() ?? string.Empty;
            bool negate = inSubqueryMatch.Groups["not"].Success;
            string subquerySql = inSubqueryMatch.Groups["subquery"].Value.Trim();
            string correlatedFields = SqlImportCteRewriteService.DescribeCorrelatedOuterReferences(subquerySql, query.OuterAliases);

            NodeViewModel inNode = new(
                NodeDefinitionRegistry.Get(NodeType.SubqueryIn),
                layout.SubqueryPosition(sourceCount)
            );
            inNode.Parameters["query"] = subquerySql;
            inNode.Parameters["negate"] = negate ? "true" : "false";
            _canvas.Nodes.Add(inNode);

            PinViewModel? valuePin = tableNodes
                .SelectMany(n => n.OutputPins)
                .FirstOrDefault(p => p.Name.Equals(leftExpr, StringComparison.OrdinalIgnoreCase));
            if (valuePin is not null)
                SqlImportClauseApplyUtilities.SafeWire(valuePin.Owner, valuePin.Name, inNode, "value", _canvas);

            SqlImportClauseApplyUtilities.SafeWire(inNode, "result", result, "where", _canvas);

            report.Add(
                new ImportReportItem(
                    negate ? "WHERE value NOT IN(sub-query)" : "WHERE value IN(sub-query)",
                    ImportItemStatus.Imported,
                    sourceNodeId: inNode.Id
                )
            );
            imported++;

            if (!string.IsNullOrWhiteSpace(correlatedFields))
            {
                report.Add(
                    new ImportReportItem(
                        "Correlation fields",
                        ImportItemStatus.Imported,
                        $"External references: {correlatedFields}",
                        inNode.Id
                    )
                );
                imported++;
            }

            return new SqlImportApplyResult(imported, partial, 0);
        }

        Match scalarSubqueryMatch = Regex.Match(
            whereClause,
            $@"^\s*(?<left>{qualifiedIdentifierPattern})\s*(?<op><>|!=|>=|<=|=|>|<)\s*\(+\s*(?<subquery>SELECT.+?)\s*\)+\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant
        );
        if (scalarSubqueryMatch.Success)
        {
            string leftExpr = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(scalarSubqueryMatch.Groups["left"].Value)
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault() ?? string.Empty;
            string op = scalarSubqueryMatch.Groups["op"].Value.Trim();
            string subquerySql = scalarSubqueryMatch.Groups["subquery"].Value.Trim();
            string correlatedFields = SqlImportCteRewriteService.DescribeCorrelatedOuterReferences(subquerySql, query.OuterAliases);

            NodeViewModel scalarNode = new(
                NodeDefinitionRegistry.Get(NodeType.SubqueryScalar),
                layout.SubqueryPosition(sourceCount)
            );
            scalarNode.Parameters["query"] = subquerySql;
            scalarNode.Parameters["operator"] = op == "!=" ? "<>" : op;
            _canvas.Nodes.Add(scalarNode);

            PinViewModel? leftPin = tableNodes
                .SelectMany(n => n.OutputPins)
                .FirstOrDefault(p => p.Name.Equals(leftExpr, StringComparison.OrdinalIgnoreCase));
            if (leftPin is not null)
                SqlImportClauseApplyUtilities.SafeWire(leftPin.Owner, leftPin.Name, scalarNode, "left", _canvas);

            SqlImportClauseApplyUtilities.SafeWire(scalarNode, "result", result, "where", _canvas);

            report.Add(
                new ImportReportItem(
                    "WHERE value op (scalar sub-query)",
                    ImportItemStatus.Imported,
                    sourceNodeId: scalarNode.Id
                )
            );
            imported++;

            if (!string.IsNullOrWhiteSpace(correlatedFields))
            {
                report.Add(
                    new ImportReportItem(
                        "Correlation fields",
                        ImportItemStatus.Imported,
                        $"External references: {correlatedFields}",
                        scalarNode.Id
                    )
                );
                imported++;
            }

            return new SqlImportApplyResult(imported, partial, 0);
        }

        Match eqMatch = Regex.Match(
            whereClause,
            $@"^\s*\(?\s*(?<left>{qualifiedIdentifierPattern})\s*(?<op><>|!=|>=|<=|=|>|<)\s*(?<right>.+?)\s*\)?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline
        );

        if (eqMatch.Success && !Regex.IsMatch(whereClause, @"\b(AND|OR)\b", RegexOptions.IgnoreCase))
        {
            string leftExpr = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(eqMatch.Groups["left"].Value)
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault() ?? string.Empty;
            string op = eqMatch.Groups["op"].Value.Trim();
            string rightExpr = eqMatch.Groups["right"].Value.Trim().Trim('\'', '"');

            NodeType compType = op switch
            {
                "=" => NodeType.Equals,
                "<>" or "!=" => NodeType.NotEquals,
                ">" => NodeType.GreaterThan,
                ">=" => NodeType.GreaterOrEqual,
                "<" => NodeType.LessThan,
                "<=" => NodeType.LessOrEqual,
                _ => NodeType.Equals,
            };

            NodeViewModel comp = new(
                NodeDefinitionRegistry.Get(compType),
                layout.ComparisonPosition(sourceCount)
            );
            comp.PinLiterals["right"] = rightExpr;
            _canvas.Nodes.Add(comp);

            NodeViewModel where = new(
                NodeDefinitionRegistry.Get(NodeType.WhereOutput),
                layout.WherePosition(sourceCount)
            );
            _canvas.Nodes.Add(where);

            PinViewModel? leftPin = tableNodes
                .SelectMany(n => n.OutputPins)
                .FirstOrDefault(p => p.Name.Equals(leftExpr, StringComparison.OrdinalIgnoreCase));
            if (leftPin is not null)
                SqlImportClauseApplyUtilities.SafeWire(leftPin.Owner, leftPin.Name, comp, "left", _canvas);

            SqlImportClauseApplyUtilities.SafeWire(comp, "result", where, "condition", _canvas);
            SqlImportClauseApplyUtilities.SafeWire(where, "result", result, "where", _canvas);

            report.Add(
                new ImportReportItem(
                    $"WHERE {leftExpr} {op} '{rightExpr}'",
                    ImportItemStatus.Imported,
                    sourceNodeId: where.Id
                )
            );
            imported++;
        }
        else
        {
            NodeViewModel where = new(
                NodeDefinitionRegistry.Get(NodeType.WhereOutput),
                layout.WherePosition(sourceCount)
            );
            _canvas.Nodes.Add(where);

            report.Add(
                new ImportReportItem(
                    $"WHERE {SqlImportClauseApplyUtilities.Truncate(query.WhereClause, 40)}",
                    ImportItemStatus.Partial,
                    "Complex condition - connect manually",
                    where.Id
                )
            );
            partial++;
        }

        return new SqlImportApplyResult(imported, partial, 0);
    }

    private static string StripOuterParentheses(string clause)
    {
        string value = clause.Trim();
        while (value.Length >= 2 && value[0] == '(' && value[^1] == ')')
        {
            int depth = 0;
            bool wrapsWholeExpression = true;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (ch == '(')
                    depth++;
                else if (ch == ')')
                    depth--;

                if (depth == 0 && i < value.Length - 1)
                {
                    wrapsWholeExpression = false;
                    break;
                }
            }

            if (!wrapsWholeExpression)
                break;

            value = value[1..^1].Trim();
        }

        return value;
    }
}
