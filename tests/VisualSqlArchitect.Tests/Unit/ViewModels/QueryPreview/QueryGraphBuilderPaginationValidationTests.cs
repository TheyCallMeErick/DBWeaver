
using Avalonia;
using DBWeaver.Core;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.Services.QueryPreview;
using static DBWeaver.Tests.Unit.ViewModels.QueryPreview.QueryPreviewTestNodeFactory;

namespace DBWeaver.Tests.Unit.ViewModels.QueryPreview;
public class QueryGraphBuilderPaginationValidationTests
{
    [Fact]
    public void BuildSql_TopWithZeroCount_ReturnsPaginationWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel top = Node(NodeType.Top);
        top.Parameters["count"] = "0";
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");
        Connect(canvas, top, "result", result, "top");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(top);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("TOP/LIMIT value must be greater than 0", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrWhiteSpace(sql));
    }

    [Fact]
    public void BuildSql_OffsetWithoutOrderBy_ReturnsDeterministicPagingWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["offset"] = "10";

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.SqlServer);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("OFFSET 10 without ORDER BY", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_OffsetWithOrderBy_DoesNotReturnDeterministicPagingWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["offset"] = "10";

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        result.Parameters["import_order_terms"] = $"{orders.Id}|id|ASC";

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.SqlServer);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.DoesNotContain(errors, e => e.Contains("without ORDER BY", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_ImportOffsetWithoutOrderBy_ReturnsDeterministicPagingWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["import_offset"] = "7";

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.MySql);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("OFFSET 7 without ORDER BY", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_TopWiredFromValueNumberWithNegativeCount_ReturnsPaginationWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel top = Node(NodeType.Top);
        NodeViewModel number = Node(NodeType.ValueNumber);
        number.Parameters["value"] = "-5";
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");
        Connect(canvas, number, "result", top, "count");
        Connect(canvas, top, "result", result, "top");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(number);
        canvas.Nodes.Add(top);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("Current value: -5", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_TopWithInvalidCount_DoesNotReturnTopPaginationWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel top = Node(NodeType.Top);
        top.Parameters["count"] = "abc";
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");
        Connect(canvas, top, "result", result, "top");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(top);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.DoesNotContain(errors, e => e.Contains("TOP/LIMIT value must be greater than 0", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_TopWithInvalidWiredCountAndValidParam_DoesNotReturnTopPaginationWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel top = Node(NodeType.Top);
        top.Parameters["count"] = "2";
        NodeViewModel number = Node(NodeType.ValueNumber);
        number.Parameters["value"] = "oops";
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");
        Connect(canvas, number, "result", top, "count");
        Connect(canvas, top, "result", result, "top");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(number);
        canvas.Nodes.Add(top);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.DoesNotContain(errors, e => e.Contains("TOP/LIMIT value must be greater than 0", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_OffsetZeroWithoutOrderBy_DoesNotReturnDeterministicPagingWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["offset"] = "0";

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.SqlServer);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.DoesNotContain(errors, e => e.Contains("without ORDER BY", StringComparison.OrdinalIgnoreCase));
    }
}




