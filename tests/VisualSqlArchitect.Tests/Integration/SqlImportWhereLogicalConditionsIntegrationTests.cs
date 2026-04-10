using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels.Canvas;
using Xunit;

namespace Integration;

public class SqlImportWhereLogicalConditionsIntegrationTests
{
    [Fact]
    public async Task ImportAsync_WithDateEquality_CreatesLiteralNodeAndWiresRightPin()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(8);
        canvas.SqlImporter.SqlInput =
            """
            SELECT [Arquivo329].*
            FROM [dbo].[Arquivo329]
            WHERE ([Arquivo329].[DataDePagamento] = '2026-03-31')
            """;

        await canvas.SqlImporter.ImportAsync();
        canvas.LiveSql.Recompile();

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.True(canvas.LiveSql.IsValid, string.Join(" | ", canvas.LiveSql.ErrorHints));

        NodeViewModel? equalsNode = canvas.Nodes.FirstOrDefault(n => n.Type == NodeType.Equals);
        Assert.NotNull(equalsNode);

        NodeViewModel? literalNode = canvas.Nodes.FirstOrDefault(n =>
            n.Type is NodeType.ValueDateTime or NodeType.ValueString);
        Assert.NotNull(literalNode);

        Assert.Contains(canvas.Connections, c =>
            c.FromPin.Owner == literalNode
            && c.FromPin.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
            && c.ToPin?.Owner == equalsNode
            && c.ToPin.Name.Equals("right", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_WithExactRefisInLiteralList_PreservesAllValues()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(8);
        canvas.SqlImporter.SqlInput =
            """
            SELECT prs.sancaoId
            FROM acd2.dbo.ProcessoRefisSancao prs
            INNER JOIN acd2.dbo.Sancao s ON s.id = prs.sancaoId
            WHERE prs.processoRefisId IN ('2630',
                            '2740',
                            '2529',
                            '2505',
                            '1388',
                            '2707',
                            '2596',
                            '2750',
                            '2714',
                            '2716',
                            '2717',
                            '2722',
                            '2679',
                            '2721',
                            '2752',
                            '2739',
                            '2746',
                            '1489',
                            '2748',
                            '2755',
                            '2700',
                            '2749',
                            '2629',
                            '2680',
                            '2734',
                            '2586',
                            '2741',
                            '1466',
                            '2502',
                            '2573',
                            '2587',
                            '2659')
            """;

        await canvas.SqlImporter.ImportAsync();
        canvas.LiveSql.Recompile();

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Equal(0, canvas.SqlImporter.ReportPartialCount);
        Assert.Equal(0, canvas.SqlImporter.ReportSkippedCount);
        Assert.True(canvas.LiveSql.IsValid, string.Join(" | ", canvas.LiveSql.ErrorHints));

        NodeViewModel? inNode = canvas.Nodes.FirstOrDefault(n => n.Type == NodeType.SubqueryIn);
        Assert.NotNull(inNode);

        string payload = inNode!.Parameters.TryGetValue("query_text", out string? queryText)
            ? queryText ?? string.Empty
            : string.Empty;

        Assert.Contains("'2630'", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'2659'", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(",", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_WithNestedAndOrAndInLiteralLists_ImportsWithoutPartial()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(8);
        canvas.SqlImporter.SqlInput =
            """
            SELECT o.id
            FROM public.orders o
            WHERE (o.id IN ('1','2','3') OR o.customer_id IN ('10','11'))
              AND o.status IS NOT NULL
            """;

        await canvas.SqlImporter.ImportAsync();
        canvas.LiveSql.Recompile();

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Equal(0, canvas.SqlImporter.ReportPartialCount);
        Assert.Equal(0, canvas.SqlImporter.ReportSkippedCount);
        Assert.True(canvas.LiveSql.IsValid, string.Join(" | ", canvas.LiveSql.ErrorHints));

        Assert.True(canvas.Nodes.Count(n => n.Type == NodeType.SubqueryIn) >= 2);
        Assert.Contains(canvas.Nodes, n => n.Type == NodeType.And);
        Assert.Contains(canvas.Nodes, n => n.Type == NodeType.Or);
        Assert.Contains(canvas.Nodes, n => n.Type == NodeType.IsNotNull);

        Assert.Contains(" IN ", canvas.LiveSql.RawSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" AND ", canvas.LiveSql.RawSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" OR ", canvas.LiveSql.RawSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportAsync_WithNotBetweenLikeAndOr_ImportsWithoutFallback()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(8);
        canvas.SqlImporter.SqlInput =
            """
            SELECT o.id
            FROM public.orders o
            WHERE NOT (o.total BETWEEN 10 AND 20 OR o.code LIKE 'AB%')
              AND (o.id = 1 OR o.id = 2)
            """;

        await canvas.SqlImporter.ImportAsync();
        canvas.LiveSql.Recompile();

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Equal(0, canvas.SqlImporter.ReportPartialCount);
        Assert.Equal(0, canvas.SqlImporter.ReportSkippedCount);
        Assert.True(canvas.LiveSql.IsValid, string.Join(" | ", canvas.LiveSql.ErrorHints));

        Assert.Contains(canvas.Nodes, n => n.Type == NodeType.Not);
        Assert.Contains(canvas.Nodes, n => n.Type == NodeType.Between);
        Assert.Contains(canvas.Nodes, n => n.Type == NodeType.Like);
        Assert.True(canvas.Nodes.Count(n => n.Type == NodeType.Or) >= 2);
        Assert.Contains(canvas.Nodes, n => n.Type == NodeType.And);

        Assert.Contains(" BETWEEN ", canvas.LiveSql.RawSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" LIKE ", canvas.LiveSql.RawSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportAsync_WithRealWorldInLiteralListAndAndOr_ImportsFully()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(8);
        canvas.SqlImporter.SqlInput =
            """
            SELECT prs.sancaoId
            FROM acd2.dbo.ProcessoRefisSancao prs
            INNER JOIN acd2.dbo.Sancao s ON s.id = prs.sancaoId
            WHERE prs.processoRefisId IN (
                '2630', '2740', '2529', '2505', '1388', '2707', '2596', '2750', '2714', '2716',
                '2717', '2722', '2679', '2721', '2752', '2739', '2746', '1489', '2748', '2755',
                '2700', '2749', '2629', '2680', '2734', '2586', '2741', '1466', '2502', '2573',
                '2587', '2659')
              AND (s.id > 0 OR s.id = prs.sancaoId)
            """;

        await canvas.SqlImporter.ImportAsync();
        canvas.LiveSql.Recompile();

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Equal(0, canvas.SqlImporter.ReportPartialCount);
        Assert.True(canvas.SqlImporter.ReportImportedCount > 0);
        Assert.True(canvas.LiveSql.IsValid, string.Join(" | ", canvas.LiveSql.ErrorHints));

        Assert.Contains(canvas.Nodes, n => n.Type == NodeType.SubqueryIn);
        Assert.Contains(canvas.Nodes, n => n.Type == NodeType.And);
        Assert.Contains(canvas.Nodes, n => n.Type == NodeType.Or);
        Assert.Contains(canvas.Nodes, n => n.Type == NodeType.GreaterThan);
        Assert.Contains(canvas.Nodes, n => n.Type == NodeType.Equals);

        Assert.Contains("processorefisid", canvas.LiveSql.RawSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" IN ", canvas.LiveSql.RawSql, StringComparison.OrdinalIgnoreCase);
    }
}
