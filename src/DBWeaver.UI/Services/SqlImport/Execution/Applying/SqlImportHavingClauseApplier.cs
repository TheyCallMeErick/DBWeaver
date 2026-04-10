using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using DBWeaver.Nodes;
using DBWeaver.UI.Services.SqlImport.Build;
using DBWeaver.UI.Services.SqlImport.Execution.Parsing;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.SqlImport.Execution.Applying;

internal sealed class SqlImportHavingClauseApplier(CanvasViewModel canvas) : ISqlImportApplyStep
{
    private readonly CanvasViewModel _canvas = canvas;

    public SqlImportApplyResult Apply(SqlImportApplyContext context)
    {
        SqlImportParsedQuery query = context.Query;
        ImportBuildContext coreContext = context.CoreContext;
        ObservableCollection<ImportReportItem> report = context.Report;
        var layout = new SqlImportLayoutCalculator(coreContext.Layout);

        if (query.HavingClause is null)
            return new SqlImportApplyResult(0, 0, 0);

        int imported = 0;
        int partial = 0;

        NodeViewModel result = coreContext.ResultNode;

        Match countHavingMatch = Regex.Match(
            query.HavingClause,
            @"^\s*\(?\s*(COUNT\s*\(\s*(?:\*|1)\s*\))\s*(<>|!=|>=|<=|=|>|<)\s*(.+?)\s*\)?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );

        if (countHavingMatch.Success)
        {
            string op = countHavingMatch.Groups[2].Value.Trim();
            string rightExpr = countHavingMatch.Groups[3].Value.Trim().Trim('\'', '"');

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

            NodeViewModel countNode = new(
                NodeDefinitionRegistry.Get(NodeType.CountStar),
                layout.HavingCountPosition(query.FromParts.Count)
            );
            _canvas.Nodes.Add(countNode);

            NodeViewModel comp = new(
                NodeDefinitionRegistry.Get(compType),
                layout.HavingComparisonPosition(query.FromParts.Count)
            );
            comp.PinLiterals["right"] = rightExpr;
            _canvas.Nodes.Add(comp);

            SqlImportClauseApplyUtilities.SafeWire(countNode, "count", comp, "left", _canvas);
            SqlImportClauseApplyUtilities.SafeWire(comp, "result", result, "having", _canvas);

            report.Add(
                new ImportReportItem(
                    $"HAVING COUNT(*) {op} {rightExpr}",
                    ImportItemStatus.Imported,
                    sourceNodeId: comp.Id
                )
            );
            imported++;
        }
        else
        {
            report.Add(
                new ImportReportItem(
                    $"HAVING {SqlImportClauseApplyUtilities.Truncate(query.HavingClause, 40)}",
                    ImportItemStatus.Partial,
                    "Complex HAVING expression - connect predicate manually",
                    result.Id
                )
            );
            partial++;
        }

        return new SqlImportApplyResult(imported, partial, 0);
    }
}
