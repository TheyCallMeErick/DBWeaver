using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using DBWeaver.Nodes;
using DBWeaver.UI.Services.SqlImport;
using DBWeaver.UI.Services.SqlImport.Build;
using DBWeaver.UI.Services.SqlImport.Execution.Parsing;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.SqlImport.Execution.Applying;

internal sealed class SqlImportOrderingClauseApplier : ISqlImportApplyStep
{
    public SqlImportApplyResult Apply(SqlImportApplyContext context)
    {
        SqlImportParsedQuery query = context.Query;
        ImportBuildContext coreContext = context.CoreContext;
        ObservableCollection<ImportReportItem> report = context.Report;
        CancellationToken cancellationToken = context.CancellationToken;

        if (query.OrderBy is null)
            return new SqlImportApplyResult(0, 0, 0);

        int imported = 0;
        int partial = 0;
        int skipped = 0;

        NodeViewModel result = coreContext.ResultNode;
        var tableNodes = coreContext.TableNodes;
        var projectedAliases = coreContext.ProjectedAliases;

        string[] terms = query.OrderBy.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        int importedTerms = 0;
        string qualifiedIdentifierPattern = SqlImportIdentifierNormalizer.QualifiedIdentifierPattern;

        foreach (string term in terms)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Match termMatch = Regex.Match(
                term,
                $@"^(?<expr>{qualifiedIdentifierPattern})(?:\s+(?<direction>ASC|DESC))?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            );

            if (!termMatch.Success)
                continue;

            string expr = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(termMatch.Groups["expr"].Value);
            string colName = expr.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? expr;
            bool desc = termMatch.Groups["direction"].Success
                && termMatch.Groups["direction"].Value.Equals("DESC", StringComparison.OrdinalIgnoreCase);

            PinViewModel? orderPin;
            if (projectedAliases.TryGetValue(colName, out PinViewModel? aliasedPin))
                orderPin = aliasedPin;
            else
            {
                orderPin = tableNodes
                    .SelectMany(n => n.OutputPins)
                    .FirstOrDefault(p => p.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));
            }

            if (orderPin is null
                && ImportBuildUtilities.TryResolveSourceAndColumn(expr, query.FromParts
                        .Select(p => new ImportFromPart(p.Table, p.Alias, p.JoinType, p.OnClause))
                        .ToList(),
                    out int sourceIndex,
                    out string inferredColumn)
                && sourceIndex >= 0
                && sourceIndex < tableNodes.Count)
            {
                orderPin = ImportBuildUtilities.EnsureOutputColumnPin(tableNodes[sourceIndex], inferredColumn);
            }

            if (orderPin is null)
                continue;

            string inputName = desc ? "order_by_desc" : "order_by";
            PinViewModel? toPin = result.InputPins.FirstOrDefault(p =>
                p.Name.Equals(inputName, StringComparison.OrdinalIgnoreCase));
            if (toPin is null)
                continue;

            coreContext.Canvas.ConnectPins(orderPin, toPin);
            importedTerms++;
        }

        if (importedTerms == terms.Length)
        {
            report.Add(new ImportReportItem($"ORDER BY {SqlImportClauseApplyUtilities.Truncate(query.OrderBy, 30)}", ImportItemStatus.Imported, sourceNodeId: result.Id));
            imported++;
        }
        else if (importedTerms > 0)
        {
            report.Add(
                new ImportReportItem(
                    $"ORDER BY {SqlImportClauseApplyUtilities.Truncate(query.OrderBy, 30)}",
                    ImportItemStatus.Partial,
                    "Some sort terms could not be mapped and were skipped",
                    result.Id
                )
            );
            partial++;
        }
        else
        {
            report.Add(
                new ImportReportItem(
                    $"ORDER BY {SqlImportClauseApplyUtilities.Truncate(query.OrderBy, 30)}",
                    ImportItemStatus.Skipped,
                    "Unsupported sort expression - add manually"
                )
            );
            skipped++;
        }

        return new SqlImportApplyResult(imported, partial, skipped);
    }
}
