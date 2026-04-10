
using Avalonia;
using DBWeaver.Core;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.Services.QueryPreview;
using static DBWeaver.Tests.Unit.ViewModels.QueryPreview.QueryPreviewTestNodeFactory;

namespace DBWeaver.Tests.Unit.ViewModels.QueryPreview;
public class QueryGraphBuilderLogicGateValidationTests
{
    [Fact]
    public void BuildSql_AndWithoutConditionsConnectedToWhere_ReturnsWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel andNode = Node(NodeType.And);
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");
        Connect(canvas, andNode, "result", result, "where");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(andNode);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("has no conditions", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_OrWithSingleConditionConnectedToWhere_ReturnsRedundantWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel isNull = Node(NodeType.IsNull);
        NodeViewModel orNode = Node(NodeType.Or);
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", isNull, "value");
        Connect(canvas, isNull, "result", orNode, "conditions");

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");
        Connect(canvas, orNode, "result", result, "where");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(isNull);
        canvas.Nodes.Add(orNode);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("only one condition", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_CompileWhereWithoutConditionsConnectedToWhere_ReturnsWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel compileWhere = Node(NodeType.CompileWhere);
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");
        Connect(canvas, compileWhere, "result", result, "where");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(compileWhere);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("COMPILE WHERE node connected to WHERE/HAVING/QUALIFY has no conditions", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_CompileWhereWithoutConditions_OnSqlServer_DoesNotEmitWhereNull()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel sancao = Table("dbo.Sancao", "nrProcessoCobranca");
        NodeViewModel compileWhere = Node(NodeType.CompileWhere);
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        NodeViewModel top = Node(NodeType.Top);
        NodeViewModel count = Node(NodeType.ValueNumber);
        count.Parameters["value"] = "4";

        Connect(canvas, sancao, "nrProcessoCobranca", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");
        Connect(canvas, compileWhere, "result", result, "where");
        Connect(canvas, top, "result", result, "top");
        Connect(canvas, count, "result", top, "count");

        canvas.Nodes.Add(sancao);
        canvas.Nodes.Add(compileWhere);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);
        canvas.Nodes.Add(top);
        canvas.Nodes.Add(count);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.SqlServer);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("COMPILE WHERE node connected to WHERE/HAVING/QUALIFY has no conditions", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("WHERE NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FETCH NEXT 4 ROWS ONLY", sql, StringComparison.OrdinalIgnoreCase);
    }
}




