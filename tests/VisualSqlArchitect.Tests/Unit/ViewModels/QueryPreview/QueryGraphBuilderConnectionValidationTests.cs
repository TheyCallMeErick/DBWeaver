using Avalonia;
using DBWeaver.Core;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.Services.QueryPreview;
using static DBWeaver.Tests.Unit.ViewModels.QueryPreview.QueryPreviewTestNodeFactory;

namespace DBWeaver.Tests.Unit.ViewModels.QueryPreview;

public class QueryGraphBuilderConnectionValidationTests
{
    [Fact]
    public void BuildSql_RowSetToScalarConnection_ReturnsCompatibilityWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        NodeViewModel subquery = Node(NodeType.Subquery);
        subquery.Parameters["query"] = "SELECT id FROM public.orders";
        subquery.Parameters["alias"] = "s";

        NodeViewModel upper = Node(NodeType.Upper);

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        Connect(canvas, subquery, "result", upper, "text");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);
        canvas.Nodes.Add(subquery);
        canvas.Nodes.Add(upper);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("Incompatible connection", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_NumberToBooleanConnection_ReturnsCompatibilityWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        NodeViewModel andNode = Node(NodeType.And);

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        Connect(canvas, orders, "id", andNode, "conditions");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);
        canvas.Nodes.Add(andNode);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("Incompatible connection", StringComparison.OrdinalIgnoreCase));
    }

}



