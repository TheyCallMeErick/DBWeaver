using Avalonia;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.QueryPreview.Services;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.QueryPreview;

public class QueryGraphBuilderSetOperationTests
{
    [Fact]
    public void BuildSql_SetOperationNodeConnectedToResultOutput_AppendsOperatorClause()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel setNode = Node(NodeType.SetOperation);
        setNode.Parameters["operator"] = "UNION ALL";
        setNode.Parameters["query"] = "SELECT id FROM public.archived_orders";
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");
        Connect(canvas, setNode, "result", result, "set_operation");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(setNode);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("union all", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("select id from public.archived_orders", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_ResultOutputWithUnion_AppendsUnionClause()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["set_operator"] = "UNION";
        result.Parameters["set_query"] = "SELECT id FROM public.archived_orders";

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("union", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("union all", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("select id from public.archived_orders", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_ResultOutputWithUnionAll_AppendsUnionAllClause()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["set_operator"] = "UNION ALL";
        result.Parameters["set_query"] = "SELECT id FROM public.archived_orders";

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("union all", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("select id from public.archived_orders", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_ResultOutputWithIntersect_AppendsIntersectClause()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["set_operator"] = "INTERSECT";
        result.Parameters["set_query"] = "SELECT id FROM public.archived_orders";

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("intersect", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("select id from public.archived_orders", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_ResultOutputWithExcept_AppendsExceptClause()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["set_operator"] = "EXCEPT";
        result.Parameters["set_query"] = "SELECT id FROM public.archived_orders";

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("except", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("select id from public.archived_orders", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_SetOperatorWithoutSetQuery_ReturnsWarningAndSkipsSetOperation()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["set_operator"] = "UNION";
        result.Parameters["set_query"] = "";

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("set_query", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("union", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_UnsupportedSetOperator_ReturnsWarningAndSkipsSetOperation()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["set_operator"] = "UNION DISTINCT";
        result.Parameters["set_query"] = "SELECT id FROM public.archived_orders";

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("not supported", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("union distinct", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_SetQueryWithoutSelectShape_ReturnsWarningAndSkipsSetOperation()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["set_operator"] = "UNION";
        result.Parameters["set_query"] = "DELETE FROM public.archived_orders";

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("must start with SELECT", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("delete from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("union", sql, StringComparison.OrdinalIgnoreCase);
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
