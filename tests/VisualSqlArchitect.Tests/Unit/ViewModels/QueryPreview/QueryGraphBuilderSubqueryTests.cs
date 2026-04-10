
using Avalonia;
using DBWeaver.Core;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.Services.QueryPreview;
using static DBWeaver.Tests.Unit.ViewModels.QueryPreview.QueryPreviewTestNodeFactory;

namespace DBWeaver.Tests.Unit.ViewModels.QueryPreview;
public class QueryGraphBuilderSubqueryTests
{
    [Fact]
    public void BuildSql_SubquerySource_UsesSubqueryAsFromClause()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel subquery = Node(NodeType.Subquery);
        subquery.Parameters["query"] = "SELECT id FROM public.orders";
        subquery.Parameters["alias"] = "o";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, subquery, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(subquery);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("(SELECT id FROM public.orders)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" o", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_SubquerySourceWithoutAlias_ReturnsWarningAndDefaultsAlias()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel subquery = Node(NodeType.Subquery);
        subquery.Parameters["query"] = "SELECT id FROM public.orders";
        subquery.Parameters["alias"] = "";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, subquery, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(subquery);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("alias is required", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(" subq", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_SubquerySourceWithoutSelectShape_ReturnsWarningAndSkipsSubquerySource()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel subquery = Node(NodeType.Subquery);
        subquery.Parameters["query"] = "DELETE FROM public.orders";
        subquery.Parameters["alias"] = "o";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, subquery, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(subquery);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("must start with SELECT", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("delete from", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_SubqueryExistsNode_EmitsExistsClauseInWhere()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel exists = Node(NodeType.SubqueryExists);
        exists.Parameters["query"] = "SELECT 1 FROM public.customers c WHERE c.id = orders.id";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");
        Connect(canvas, exists, "result", result, "where");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(exists);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("where", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exists", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("select 1 from public.customers", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_SubqueryExistsWithoutQuery_ReturnsWarningAndUsesSelectOneFallback()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel exists = Node(NodeType.SubqueryExists);
        exists.Parameters["query"] = "";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");
        Connect(canvas, exists, "result", result, "where");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(exists);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("defaulting to select 1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("exists", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("select 1", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_SubqueryExistsWithoutSelectShape_ReturnsWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel exists = Node(NodeType.SubqueryExists);
        exists.Parameters["query"] = "DELETE FROM public.customers";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");
        Connect(canvas, exists, "result", result, "where");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(exists);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("must start with SELECT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_SubqueryInNode_EmitsInSubqueryInWhere()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel inNode = Node(NodeType.SubqueryIn);
        inNode.Parameters["query"] = "SELECT id FROM public.customers";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", inNode, "value");
        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");
        Connect(canvas, inNode, "result", result, "where");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(inNode);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains(" in ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("select id from public.customers", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_SubqueryScalarNode_EmitsComparisonWithScalarSubquery()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel scalar = Node(NodeType.SubqueryScalar);
        scalar.Parameters["operator"] = ">";
        scalar.Parameters["query"] = "SELECT AVG(id) FROM public.customers";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", scalar, "left");
        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");
        Connect(canvas, scalar, "result", result, "where");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(scalar);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains(">", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("select avg(id) from public.customers", sql, StringComparison.OrdinalIgnoreCase);
    }
}




