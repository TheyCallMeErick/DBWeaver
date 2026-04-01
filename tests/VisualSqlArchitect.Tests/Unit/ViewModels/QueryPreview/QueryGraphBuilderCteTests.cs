using Avalonia;
using System.Text.RegularExpressions;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Serialization;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.QueryPreview.Services;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.QueryPreview;

public class QueryGraphBuilderCteTests
{
    [Fact]
    public void BuildSql_CteQuery_InfersFromTableFromLocalUpstreamGraph()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel unrelated = Table("public.unrelated", "id");
        NodeViewModel orders = Table("public.orders", "id");

        NodeViewModel cteColumnList = Node(NodeType.ColumnList);
        NodeViewModel cteResult = Node(NodeType.ResultOutput);
        NodeViewModel cteDef = Node(NodeType.CteDefinition);
        cteDef.Parameters["name"] = "orders_cte";

        Connect(canvas, orders, "id", cteColumnList, "columns");
        Connect(canvas, cteColumnList, "result", cteResult, "columns");
        Connect(canvas, cteResult, "result", cteDef, "query");

        NodeViewModel cteSource = Node(NodeType.CteSource);
        cteSource.Parameters["cte_name"] = "orders_cte";

        NodeViewModel mainColumnList = Node(NodeType.ColumnList);
        NodeViewModel mainResult = Node(NodeType.ResultOutput);
        Connect(canvas, cteSource, "result", mainColumnList, "columns");
        Connect(canvas, mainColumnList, "result", mainResult, "columns");

        canvas.Nodes.Add(unrelated);
        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(cteColumnList);
        canvas.Nodes.Add(cteResult);
        canvas.Nodes.Add(cteDef);
        canvas.Nodes.Add(cteSource);
        canvas.Nodes.Add(mainColumnList);
        canvas.Nodes.Add(mainResult);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("orders_cte", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orders", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_CteQuery_DoesNotLeakUnrelatedTableIntoCteBody()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel unrelated = Table("public.unrelated", "id");
        NodeViewModel orders = Table("public.orders", "id");

        NodeViewModel cteColumnList = Node(NodeType.ColumnList);
        NodeViewModel cteResult = Node(NodeType.ResultOutput);
        NodeViewModel cteDef = Node(NodeType.CteDefinition);
        cteDef.Parameters["name"] = "orders_cte";

        Connect(canvas, orders, "id", cteColumnList, "columns");
        Connect(canvas, cteColumnList, "result", cteResult, "columns");
        Connect(canvas, cteResult, "result", cteDef, "query");

        NodeViewModel cteSource = Node(NodeType.CteSource);
        cteSource.Parameters["cte_name"] = "orders_cte";

        NodeViewModel mainColumnList = Node(NodeType.ColumnList);
        NodeViewModel mainResult = Node(NodeType.ResultOutput);
        Connect(canvas, cteSource, "result", mainColumnList, "columns");
        Connect(canvas, mainColumnList, "result", mainResult, "columns");

        canvas.Nodes.Add(unrelated);
        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(cteColumnList);
        canvas.Nodes.Add(cteResult);
        canvas.Nodes.Add(cteDef);
        canvas.Nodes.Add(cteSource);
        canvas.Nodes.Add(mainColumnList);
        canvas.Nodes.Add(mainResult);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Match bodyMatch = Regex.Match(
            sql,
            @"(?is)\bas\s*\((.*?)\)\s*select",
            RegexOptions.CultureInvariant
        );
        Assert.True(bodyMatch.Success, "Expected SQL to contain an extractable CTE body. SQL: " + sql);

        string cteBody = bodyMatch.Groups[1].Value;
        Assert.DoesNotContain("unrelated", cteBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_CteWithoutLocalSource_IsSkippedAndReportsUndefinedCte()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel unrelated = Table("public.unrelated", "id");

        NodeViewModel literal = Node(NodeType.ValueNumber);
        literal.Parameters["value"] = "1";

        NodeViewModel cteColumnList = Node(NodeType.ColumnList);
        NodeViewModel cteResult = Node(NodeType.ResultOutput);
        NodeViewModel cteDef = Node(NodeType.CteDefinition);
        cteDef.Parameters["name"] = "dangling_cte";

        Connect(canvas, literal, "result", cteColumnList, "columns");
        Connect(canvas, cteColumnList, "result", cteResult, "columns");
        Connect(canvas, cteResult, "result", cteDef, "query");

        NodeViewModel cteSource = Node(NodeType.CteSource);
        cteSource.Parameters["cte_name"] = "dangling_cte";

        NodeViewModel mainColumnList = Node(NodeType.ColumnList);
        NodeViewModel mainResult = Node(NodeType.ResultOutput);
        Connect(canvas, cteSource, "result", mainColumnList, "columns");
        Connect(canvas, mainColumnList, "result", mainResult, "columns");

        canvas.Nodes.Add(unrelated);
        canvas.Nodes.Add(literal);
        canvas.Nodes.Add(cteColumnList);
        canvas.Nodes.Add(cteResult);
        canvas.Nodes.Add(cteDef);
        canvas.Nodes.Add(cteSource);
        canvas.Nodes.Add(mainColumnList);
        canvas.Nodes.Add(mainResult);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.DoesNotContain("with", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(errors, e => e.Contains("undefined CTE 'dangling_cte'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_CteWithPersistedSubgraph_GeneratesSqlWithoutLeakingSubgraphNodes()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel cteDef = Node(NodeType.CteDefinition);
        cteDef.Parameters["name"] = "orders_cte";
        cteDef.Parameters[CanvasSerializer.CteSubgraphParameterKey] = BuildPersistedCteSubgraphJson();

        NodeViewModel cteSource = Node(NodeType.CteSource);
        cteSource.Parameters["cte_name"] = "orders_cte";

        NodeViewModel mainColumnList = Node(NodeType.ColumnList);
        NodeViewModel mainResult = Node(NodeType.ResultOutput);
        Connect(canvas, cteSource, "result", mainColumnList, "columns");
        Connect(canvas, mainColumnList, "result", mainResult, "columns");

        canvas.Nodes.Add(cteDef);
        canvas.Nodes.Add(cteSource);
        canvas.Nodes.Add(mainColumnList);
        canvas.Nodes.Add(mainResult);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("with", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orders_cte", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orders", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_RecursiveCteWithPersistedSubgraph_AddsWithRecursivePrefix()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel cteDef = Node(NodeType.CteDefinition);
        cteDef.Parameters["name"] = "orders_cte";
        cteDef.Parameters["recursive"] = "true";
        cteDef.Parameters[CanvasSerializer.CteSubgraphParameterKey] = BuildPersistedCteSubgraphJson();

        NodeViewModel cteSource = Node(NodeType.CteSource);
        cteSource.Parameters["cte_name"] = "orders_cte";

        NodeViewModel mainColumnList = Node(NodeType.ColumnList);
        NodeViewModel mainResult = Node(NodeType.ResultOutput);
        Connect(canvas, cteSource, "result", mainColumnList, "columns");
        Connect(canvas, mainColumnList, "result", mainResult, "columns");

        canvas.Nodes.Add(cteDef);
        canvas.Nodes.Add(cteSource);
        canvas.Nodes.Add(mainColumnList);
        canvas.Nodes.Add(mainResult);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.StartsWith("WITH RECURSIVE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orders_cte", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_CteSourceAlias_UsesAliasedFromReference()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel cteDef = Node(NodeType.CteDefinition);
        cteDef.Parameters["name"] = "orders_cte";
        cteDef.Parameters[CanvasSerializer.CteSubgraphParameterKey] = BuildPersistedCteSubgraphJson();

        NodeViewModel cteSource = Node(NodeType.CteSource);
        cteSource.Parameters["cte_name"] = "orders_cte";
        cteSource.Parameters["alias"] = "pe";

        NodeViewModel mainColumnList = Node(NodeType.ColumnList);
        NodeViewModel mainResult = Node(NodeType.ResultOutput);
        Connect(canvas, cteSource, "result", mainColumnList, "columns");
        Connect(canvas, mainColumnList, "result", mainResult, "columns");

        canvas.Nodes.Add(cteDef);
        canvas.Nodes.Add(cteSource);
        canvas.Nodes.Add(mainColumnList);
        canvas.Nodes.Add(mainResult);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Matches(
            new Regex("from\\s+(?:\"orders_cte\"(?:\\s+as)?\\s+\"pe\"|\"orders_cte pe\")", RegexOptions.IgnoreCase),
            sql
        );
    }

    [Fact]
    public void BuildSql_ResultOutputHavingBinding_EmitsHavingClause()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "total");
        NodeViewModel sum = Node(NodeType.Sum);
        NodeViewModel gt = Node(NodeType.GreaterThan);
        NodeViewModel threshold = Node(NodeType.ValueNumber);
        threshold.Parameters["value"] = "100";

        NodeViewModel mainColumnList = Node(NodeType.ColumnList);
        NodeViewModel mainResult = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "total", sum, "value");
        Connect(canvas, sum, "total", gt, "left");
        Connect(canvas, threshold, "result", gt, "right");
        Connect(canvas, sum, "total", mainColumnList, "columns");
        Connect(canvas, mainColumnList, "result", mainResult, "columns");
        Connect(canvas, gt, "result", mainResult, "having");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(sum);
        canvas.Nodes.Add(gt);
        canvas.Nodes.Add(threshold);
        canvas.Nodes.Add(mainColumnList);
        canvas.Nodes.Add(mainResult);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("HAVING", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSql_ResultOutputDistinctParameter_EmitsSelectDistinct()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel mainColumnList = Node(NodeType.ColumnList);
        NodeViewModel mainResult = Node(NodeType.ResultOutput);
        mainResult.Parameters["distinct"] = "true";

        Connect(canvas, orders, "id", mainColumnList, "columns");
        Connect(canvas, mainColumnList, "result", mainResult, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(mainColumnList);
        canvas.Nodes.Add(mainResult);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("SELECT DISTINCT", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_WithTopNode_EmitsLiteralLimitWithoutBoundPlaceholder()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel mainColumnList = Node(NodeType.ColumnList);
        NodeViewModel mainResult = Node(NodeType.ResultOutput);
        NodeViewModel top = Node(NodeType.Top);
        NodeViewModel count = Node(NodeType.ValueNumber);
        count.Parameters["value"] = "25";

        Connect(canvas, orders, "id", mainColumnList, "columns");
        Connect(canvas, mainColumnList, "result", mainResult, "columns");
        Connect(canvas, top, "result", mainResult, "top");
        Connect(canvas, count, "result", top, "count");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(mainColumnList);
        canvas.Nodes.Add(mainResult);
        canvas.Nodes.Add(top);
        canvas.Nodes.Add(count);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.DoesNotContain("@p", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":p", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT 25", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_WithTopNode_OnSqlServer_DoesNotEmitLimitClause()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("dbo.orders", "id");
        NodeViewModel mainColumnList = Node(NodeType.ColumnList);
        NodeViewModel mainResult = Node(NodeType.ResultOutput);
        NodeViewModel top = Node(NodeType.Top);
        NodeViewModel count = Node(NodeType.ValueNumber);
        count.Parameters["value"] = "25";

        Connect(canvas, orders, "id", mainColumnList, "columns");
        Connect(canvas, mainColumnList, "result", mainResult, "columns");
        Connect(canvas, top, "result", mainResult, "top");
        Connect(canvas, count, "result", top, "count");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(mainColumnList);
        canvas.Nodes.Add(mainResult);
        canvas.Nodes.Add(top);
        canvas.Nodes.Add(count);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.SqlServer);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.DoesNotContain("LIMIT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Matches(
            new Regex(@"TOP\s*\(?25\)?|FETCH\s+NEXT\s+25\s+ROWS\s+ONLY", RegexOptions.IgnoreCase),
            sql
        );
    }

    [Fact]
    public void BuildSql_CteDefinitionWithInvalidName_AddsValidationError()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel cteDef = Node(NodeType.CteDefinition);
        cteDef.Parameters["name"] = "orders-cte";
        cteDef.Parameters[CanvasSerializer.CteSubgraphParameterKey] = BuildPersistedCteSubgraphJson();

        NodeViewModel cteSource = Node(NodeType.CteSource);
        cteSource.Parameters["cte_name"] = "orders-cte";

        NodeViewModel mainColumnList = Node(NodeType.ColumnList);
        NodeViewModel mainResult = Node(NodeType.ResultOutput);
        Connect(canvas, cteSource, "result", mainColumnList, "columns");
        Connect(canvas, mainColumnList, "result", mainResult, "columns");

        canvas.Nodes.Add(cteDef);
        canvas.Nodes.Add(cteSource);
        canvas.Nodes.Add(mainColumnList);
        canvas.Nodes.Add(mainResult);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("is invalid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_CteSelfReferenceWithoutRecursive_AddsDiagnosticError()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel cteDef = Node(NodeType.CteDefinition);
        cteDef.Parameters["name"] = "self_cte";
        cteDef.Parameters["source_table"] = "self_cte";
        cteDef.Parameters[CanvasSerializer.CteSubgraphParameterKey] = BuildPersistedCteSubgraphJson();

        NodeViewModel cteSource = Node(NodeType.CteSource);
        cteSource.Parameters["cte_name"] = "self_cte";

        NodeViewModel mainColumnList = Node(NodeType.ColumnList);
        NodeViewModel mainResult = Node(NodeType.ResultOutput);
        Connect(canvas, cteSource, "result", mainColumnList, "columns");
        Connect(canvas, mainColumnList, "result", mainResult, "columns");

        canvas.Nodes.Add(cteDef);
        canvas.Nodes.Add(cteSource);
        canvas.Nodes.Add(mainColumnList);
        canvas.Nodes.Add(mainResult);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("not marked recursive", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("requires the 'recursive' flag", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_CteMutualCycle_AddsCycleDiagnosticError()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel cteA = Node(NodeType.CteDefinition);
        cteA.Parameters["name"] = "cte_a";
        cteA.Parameters["source_table"] = "cte_b";
        cteA.Parameters[CanvasSerializer.CteSubgraphParameterKey] = BuildPersistedCteSubgraphJson();

        NodeViewModel cteB = Node(NodeType.CteDefinition);
        cteB.Parameters["name"] = "cte_b";
        cteB.Parameters["source_table"] = "cte_a";
        cteB.Parameters[CanvasSerializer.CteSubgraphParameterKey] = BuildPersistedCteSubgraphJson();

        NodeViewModel cteSource = Node(NodeType.CteSource);
        cteSource.Parameters["cte_name"] = "cte_a";

        NodeViewModel mainColumnList = Node(NodeType.ColumnList);
        NodeViewModel mainResult = Node(NodeType.ResultOutput);
        Connect(canvas, cteSource, "result", mainColumnList, "columns");
        Connect(canvas, mainColumnList, "result", mainResult, "columns");

        canvas.Nodes.Add(cteA);
        canvas.Nodes.Add(cteB);
        canvas.Nodes.Add(cteSource);
        canvas.Nodes.Add(mainColumnList);
        canvas.Nodes.Add(mainResult);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("Cycle detected between CTE definitions", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("CTE cycle detected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_ReferenceLevelComposition_EmitsCoreComplexQueryClauses()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel cteA = Node(NodeType.CteDefinition);
        cteA.Parameters["name"] = "boletos_vencidos";
        cteA.Parameters[CanvasSerializer.CteSubgraphParameterKey] = BuildPersistedCteSubgraphJson();

        NodeViewModel cteB = Node(NodeType.CteDefinition);
        cteB.Parameters["name"] = "processos_elegiveis";
        cteB.Parameters["source_table"] = "boletos_vencidos";
        cteB.Parameters[CanvasSerializer.CteSubgraphParameterKey] = BuildPersistedCteSubgraphJson();

        NodeViewModel cteSource = Node(NodeType.CteSource);
        cteSource.Parameters["cte_name"] = "processos_elegiveis";
        cteSource.Parameters["alias"] = "pe";

        NodeViewModel boleto = Table("public.boleto", "id", "id_processo_refis", "parcela");
        NodeViewModel pessoa = Table("public.vw_pessoa", "id", "nome");

        NodeViewModel join = Node(NodeType.Join);
        join.Parameters["join_type"] = "INNER";

        NodeViewModel window = Node(NodeType.WindowFunction);
        window.Parameters["function"] = "RowNumber";

        NodeViewModel stringAgg = Node(NodeType.StringAgg);
        stringAgg.Parameters["separator"] = ", ";

        NodeViewModel textLiteral = Node(NodeType.ValueString);
        textLiteral.Parameters["value"] = "proc";

        NodeViewModel count = Node(NodeType.CountStar);
        NodeViewModel gt = Node(NodeType.GreaterThan);
        NodeViewModel zero = Node(NodeType.ValueNumber);
        zero.Parameters["value"] = "0";

        NodeViewModel colList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["distinct"] = "true";

        Connect(canvas, boleto, "id_processo_refis", join, "left");
        Connect(canvas, pessoa, "id", join, "right");

        Connect(canvas, boleto, "parcela", window, "order_1");
        Connect(canvas, textLiteral, "result", stringAgg, "value");

        Connect(canvas, count, "count", gt, "left");
        Connect(canvas, zero, "result", gt, "right");

        Connect(canvas, cteSource, "result", colList, "columns");
        Connect(canvas, stringAgg, "result", colList, "columns");
        Connect(canvas, window, "result", colList, "columns");
        Connect(canvas, count, "count", colList, "columns");

        Connect(canvas, colList, "result", result, "columns");
        Connect(canvas, gt, "result", result, "having");

        canvas.Nodes.Add(cteA);
        canvas.Nodes.Add(cteB);
        canvas.Nodes.Add(cteSource);
        canvas.Nodes.Add(boleto);
        canvas.Nodes.Add(pessoa);
        canvas.Nodes.Add(join);
        canvas.Nodes.Add(window);
        canvas.Nodes.Add(stringAgg);
        canvas.Nodes.Add(textLiteral);
        canvas.Nodes.Add(count);
        canvas.Nodes.Add(gt);
        canvas.Nodes.Add(zero);
        canvas.Nodes.Add(colList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("WITH", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SELECT DISTINCT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JOIN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROW_NUMBER() OVER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STRING_AGG", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HAVING", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildPersistedCteSubgraphJson()
    {
        var nodes = new List<SavedNode>
        {
            new(
                NodeId: "table1",
                NodeType: nameof(NodeType.TableSource),
                X: 0,
                Y: 0,
                ZOrder: null,
                Alias: null,
                TableFullName: "public.orders",
                Parameters: new Dictionary<string, string>(),
                PinLiterals: new Dictionary<string, string>(),
                Columns: new List<SavedColumn> { new("id", nameof(PinDataType.Number)) }
            ),
            new(
                NodeId: "collist1",
                NodeType: nameof(NodeType.ColumnList),
                X: 140,
                Y: 0,
                ZOrder: null,
                Alias: null,
                TableFullName: null,
                Parameters: new Dictionary<string, string>(),
                PinLiterals: new Dictionary<string, string>()
            ),
            new(
                NodeId: "result1",
                NodeType: nameof(NodeType.ResultOutput),
                X: 280,
                Y: 0,
                ZOrder: null,
                Alias: null,
                TableFullName: null,
                Parameters: new Dictionary<string, string>(),
                PinLiterals: new Dictionary<string, string>()
            )
        };

        var connections = new List<SavedConnection>
        {
            new("table1", "id", "collist1", "columns"),
            new("collist1", "result", "result1", "columns")
        };

        var subgraph = new SavedCteSubgraph(nodes, connections, "result1");
        return System.Text.Json.JsonSerializer.Serialize(subgraph);
    }

    private static NodeViewModel Node(NodeType type) =>
        new(NodeDefinitionRegistry.Get(type), new Point(0, 0));

    private static NodeViewModel Table(string tableName, params string[] columns) =>
        new(tableName, columns.Select(c => (c, PinDataType.Number)), new Point(0, 0));

    private static void Connect(
        CanvasViewModel canvas,
        NodeViewModel fromNode,
        string fromPin,
        NodeViewModel toNode,
        string toPin)
    {
        PinViewModel from = fromNode.OutputPins.First(p => p.Name == fromPin);
        PinViewModel to = toNode.InputPins.First(p => p.Name == toPin);

        canvas.Connections.Add(new ConnectionViewModel(from, from.AbsolutePosition, to.AbsolutePosition)
        {
            ToPin = to,
        });
    }
}
