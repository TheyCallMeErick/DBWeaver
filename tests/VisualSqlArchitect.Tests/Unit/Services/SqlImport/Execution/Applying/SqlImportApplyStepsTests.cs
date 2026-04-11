using System.Collections.ObjectModel;
using DBWeaver.Nodes;
using DBWeaver.UI.Services.SqlImport.Build;
using DBWeaver.UI.Services.SqlImport.Execution.Applying;
using DBWeaver.UI.Services.SqlImport.Execution.Parsing;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.Services.SqlImport.Execution.Applying;

public class SqlImportApplyStepsTests
{
    [Fact]
    public void WhereClauseApplier_WithSimpleComparison_ImportsWhereChain()
    {
        var setup = CreateCoreContext();
        var query = CreateQuery(whereClause: "id = 10");
        var context = new SqlImportApplyContext(query, setup.CoreContext, setup.Report, CancellationToken.None);

        var step = new SqlImportWhereClauseApplier(setup.Canvas);

        SqlImportApplyResult result = step.Apply(context);

        Assert.Equal(1, result.Imported);
        Assert.Contains(setup.Canvas.Nodes, n =>
            n.Type is NodeType.Equals
                or NodeType.NotEquals
                or NodeType.GreaterThan
                or NodeType.GreaterOrEqual
                or NodeType.LessThan
                or NodeType.LessOrEqual);
        Assert.Contains(setup.Canvas.Connections, c =>
            c.ToPin?.Owner == setup.CoreContext.ResultNode
            && c.ToPin.Name.Equals("where", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OrderingClauseApplier_WithProjectedAlias_CreatesOrderByConnection()
    {
        var setup = CreateCoreContext(selectedColumns: [new ImportSelectTerm("id", "order_id")]);
        var query = CreateQuery(orderBy: "order_id DESC", selectedColumns: [new SqlImportSelectedColumn("id", "order_id")]);
        var context = new SqlImportApplyContext(query, setup.CoreContext, setup.Report, CancellationToken.None);

        var step = new SqlImportOrderingClauseApplier();

        SqlImportApplyResult result = step.Apply(context);

        Assert.Equal(1, result.Imported);
        Assert.Contains(setup.Canvas.Connections, c =>
            c.ToPin?.Owner == setup.CoreContext.ResultNode
            && c.ToPin.Name.Equals("order_by_desc", StringComparison.OrdinalIgnoreCase)
            && c.FromPin.Name.Equals("id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GroupingClauseApplier_WithMappedTerms_CreatesGroupByConnection()
    {
        var setup = CreateCoreContext(selectedColumns: [new ImportSelectTerm("id", null)]);
        var query = CreateQuery(groupBy: "id", selectedColumns: [new SqlImportSelectedColumn("id", null)]);
        var context = new SqlImportApplyContext(query, setup.CoreContext, setup.Report, CancellationToken.None);

        var step = new SqlImportGroupingClauseApplier();

        SqlImportApplyResult result = step.Apply(context);

        Assert.Equal(1, result.Imported);
        Assert.Contains(setup.Canvas.Connections, c =>
            c.ToPin?.Owner == setup.CoreContext.ResultNode
            && c.ToPin.Name.Equals("group_by", StringComparison.OrdinalIgnoreCase)
            && c.FromPin.Name.Equals("id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HavingClauseApplier_WithCountComparison_AddsPredicateNodes()
    {
        var setup = CreateCoreContext();
        var query = CreateQuery(havingClause: "COUNT(*) > 1", groupBy: "id");
        var context = new SqlImportApplyContext(query, setup.CoreContext, setup.Report, CancellationToken.None);

        var step = new SqlImportHavingClauseApplier(setup.Canvas);

        SqlImportApplyResult result = step.Apply(context);

        Assert.Equal(1, result.Imported);
        Assert.Contains(setup.Canvas.Nodes, n => n.Type == NodeType.CountStar);
        Assert.Contains(setup.Canvas.Nodes, n => n.Type == NodeType.GreaterThan);
    }

    [Fact]
    public void ResultModifiersApplier_WithLimitAndDistinct_ImportsBothModifiers()
    {
        var setup = CreateCoreContext();
        var query = CreateQuery(limit: 5, isDistinct: true);
        var context = new SqlImportApplyContext(query, setup.CoreContext, setup.Report, CancellationToken.None);

        var step = new SqlImportResultModifiersApplier(setup.Canvas);

        SqlImportApplyResult result = step.Apply(context);

        Assert.Equal(2, result.Imported);
        Assert.Equal("true", setup.CoreContext.ResultNode.Parameters["distinct"]);
        Assert.Contains(setup.Canvas.Nodes, n => n.Type == NodeType.Top);
    }

    private static (CanvasViewModel Canvas, ImportBuildContext CoreContext, ObservableCollection<ImportReportItem> Report) CreateCoreContext(
        IReadOnlyList<ImportSelectTerm>? selectedColumns = null
    )
    {
        var canvas = new CanvasViewModel();
        var report = new ObservableCollection<ImportReportItem>();
        var builder = new ImportModelToCanvasBuilder(canvas);

        var buildInput = new ImportBuildInput(
            [new ImportFromPart("public.orders", null, null, null)],
            selectedColumns ?? [new ImportSelectTerm("id", null)],
            IsStar: false,
            StarQualifier: null,
            SqlImportLayoutPolicy.Default
        );

        ImportBuildContext core = builder.BuildCore(buildInput, report, CancellationToken.None);
        return (canvas, core, report);
    }

    private static SqlImportParsedQuery CreateQuery(
        string? whereClause = null,
        string? orderBy = null,
        string? groupBy = null,
        string? havingClause = null,
        int? limit = null,
        bool isDistinct = false,
        IReadOnlyList<SqlImportSelectedColumn>? selectedColumns = null
    ) =>
        new(
            isDistinct,
            IsStar: false,
            StarQualifier: null,
            selectedColumns ?? [new SqlImportSelectedColumn("id", null)],
            [new SqlImportSourcePart("public.orders", null, null, null)],
            whereClause,
            orderBy,
            groupBy,
            havingClause,
            limit,
            OuterAliases: []
        );
}
