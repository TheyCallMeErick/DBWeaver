
using Avalonia;
using DBWeaver.Core;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.Services.QueryPreview;
using static DBWeaver.Tests.Unit.ViewModels.QueryPreview.QueryPreviewTestNodeFactory;

namespace DBWeaver.Tests.Unit.ViewModels.QueryPreview;
public class QueryGraphBuilderQualifyTests
{
    [Fact]
    public void BuildSql_QualifyConnected_WrapsFinalQueryWithOuterFilter()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id", "amount");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel greater = Node(NodeType.GreaterThan);
        NodeViewModel value = Node(NodeType.ValueNumber);
        value.Parameters["value"] = "100";
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "amount", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        Connect(canvas, orders, "amount", greater, "left");
        Connect(canvas, value, "result", greater, "right");
        Connect(canvas, greater, "result", result, "qualify");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(greater);
        canvas.Nodes.Add(value);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("AS _qualify", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("> 100", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_InvalidComparisonInQualify_ReturnsValidationError()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id", "amount");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel equals = Node(NodeType.Equals);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        Connect(canvas, orders, "amount", equals, "left");
        Connect(canvas, equals, "result", result, "qualify");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(equals);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(
            errors,
            e =>
                e.Contains("WHERE/HAVING/QUALIFY", StringComparison.OrdinalIgnoreCase)
                && e.Contains("missing required input 'right'", StringComparison.OrdinalIgnoreCase)
        );
    }
}




