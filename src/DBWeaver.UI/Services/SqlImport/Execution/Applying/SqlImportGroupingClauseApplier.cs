using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using DBWeaver.UI.Services.SqlImport;
using DBWeaver.UI.Services.SqlImport.Build;
using DBWeaver.UI.Services.SqlImport.Execution.Parsing;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.SqlImport.Execution.Applying;

internal sealed class SqlImportGroupingClauseApplier : ISqlImportApplyStep
{
    public SqlImportApplyResult Apply(SqlImportApplyContext context)
    {
        SqlImportParsedQuery query = context.Query;
        ImportBuildContext coreContext = context.CoreContext;
        ObservableCollection<ImportReportItem> report = context.Report;
        CancellationToken cancellationToken = context.CancellationToken;

        if (query.GroupBy is null)
            return new SqlImportApplyResult(0, 0, 0);

        int imported = 0;
        int partial = 0;
        int skipped = 0;

        NodeViewModel result = coreContext.ResultNode;
        var tableNodes = coreContext.TableNodes;
        var projectedAliases = coreContext.ProjectedAliases;

        string[] terms = query.GroupBy.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        string qualifiedIdentifierPattern = SqlImportIdentifierNormalizer.QualifiedIdentifierPattern;

        int importedTerms = 0;

        foreach (string term in terms)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Match termMatch = Regex.Match(
                term,
                $@"^(?<expr>{qualifiedIdentifierPattern})$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!termMatch.Success)
                continue;

            string expr = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(termMatch.Groups["expr"].Value);
            string colName = expr.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? expr;

            PinViewModel? groupPin;
            if (projectedAliases.TryGetValue(colName, out PinViewModel? aliasedPin))
                groupPin = aliasedPin;
            else
            {
                groupPin = tableNodes
                    .SelectMany(n => n.OutputPins)
                    .FirstOrDefault(p => p.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));
            }

            if (groupPin is null)
                continue;

            PinViewModel? toPin = result.InputPins.FirstOrDefault(p =>
                p.Name.Equals("group_by", StringComparison.OrdinalIgnoreCase));
            if (toPin is null)
                continue;

            coreContext.Canvas.ConnectPins(groupPin, toPin);
            importedTerms++;
        }

        if (importedTerms == terms.Length)
        {
            report.Add(new ImportReportItem($"GROUP BY {SqlImportClauseApplyUtilities.Truncate(query.GroupBy, 30)}", ImportItemStatus.Imported, sourceNodeId: result.Id));
            imported++;
        }
        else if (importedTerms > 0)
        {
            report.Add(
                new ImportReportItem(
                    $"GROUP BY {SqlImportClauseApplyUtilities.Truncate(query.GroupBy, 30)}",
                    ImportItemStatus.Partial,
                    "Some grouping terms could not be mapped and were skipped",
                    result.Id
                )
            );
            partial++;
        }
        else
        {
            report.Add(
                new ImportReportItem(
                    $"GROUP BY {SqlImportClauseApplyUtilities.Truncate(query.GroupBy, 30)}",
                    ImportItemStatus.Skipped,
                    "Unsupported grouping expression - add manually"
                )
            );
            skipped++;
        }

        var groupedTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string term in terms)
        {
            Match groupedTermMatch = Regex.Match(
                term,
                $@"^(?<expr>{qualifiedIdentifierPattern})$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!groupedTermMatch.Success)
                continue;

            string groupedExpr = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(groupedTermMatch.Groups["expr"].Value);
            groupedTerms.Add(groupedExpr);
            groupedTerms.Add(groupedExpr.Split('.').Last());
        }

        foreach (SqlImportSelectedColumn selectedColumn in query.SelectedColumns)
        {
            if (SqlImportClauseApplyUtilities.LooksLikeAggregateExpression(selectedColumn.Expr))
                continue;

            string exprTrimmed = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(selectedColumn.Expr);
            string exprShort = exprTrimmed.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? exprTrimmed;
            bool isGrouped = groupedTerms.Contains(exprTrimmed)
                || groupedTerms.Contains(exprShort)
                || (!string.IsNullOrWhiteSpace(selectedColumn.Alias) && groupedTerms.Contains(selectedColumn.Alias.Trim()));

            if (isGrouped)
                continue;

            report.Add(
                new ImportReportItem(
                    $"GROUP BY conflict: {SqlImportClauseApplyUtilities.Truncate(exprTrimmed, 40)}",
                    ImportItemStatus.Partial,
                    "Selected column is neither grouped nor aggregated"
                )
            );
            partial++;
        }

        return new SqlImportApplyResult(imported, partial, skipped);
    }
}
