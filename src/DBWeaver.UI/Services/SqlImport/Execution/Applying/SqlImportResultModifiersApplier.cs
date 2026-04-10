using System.Collections.ObjectModel;
using DBWeaver.Nodes;
using DBWeaver.UI.Services.SqlImport.Build;
using DBWeaver.UI.Services.SqlImport.Execution.Parsing;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.SqlImport.Execution.Applying;

internal sealed class SqlImportResultModifiersApplier(CanvasViewModel canvas) : ISqlImportApplyStep
{
    private readonly CanvasViewModel _canvas = canvas;

    public SqlImportApplyResult Apply(SqlImportApplyContext context)
    {
        SqlImportParsedQuery query = context.Query;
        ImportBuildContext coreContext = context.CoreContext;
        ObservableCollection<ImportReportItem> report = context.Report;
        var layout = new SqlImportLayoutCalculator(coreContext.Layout);

        int imported = 0;

        NodeViewModel result = coreContext.ResultNode;
        double resultY = coreContext.ResultY;

        if (query.Limit.HasValue)
        {
            NodeViewModel top = new(
                NodeDefinitionRegistry.Get(NodeType.Top),
                layout.TopPosition(resultY)
            );
            top.Parameters["count"] = query.Limit.Value.ToString();
            _canvas.Nodes.Add(top);
            SqlImportClauseApplyUtilities.SafeWire(top, "result", result, "top", _canvas);
            report.Add(new ImportReportItem($"LIMIT {query.Limit}", ImportItemStatus.Imported, sourceNodeId: top.Id));
            imported++;
        }

        if (query.IsDistinct)
        {
            result.Parameters["distinct"] = "true";
            report.Add(
                new ImportReportItem(
                    "SELECT DISTINCT",
                    ImportItemStatus.Imported,
                    "ResultOutput distinct flag enabled",
                    result.Id
                )
            );
            imported++;
        }

        return new SqlImportApplyResult(imported, 0, 0);
    }
}
