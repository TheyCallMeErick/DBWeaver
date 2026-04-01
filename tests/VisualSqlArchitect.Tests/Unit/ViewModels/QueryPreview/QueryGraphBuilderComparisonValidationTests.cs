using Avalonia;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.QueryPreview.Services;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.QueryPreview;

public class QueryGraphBuilderComparisonValidationTests
{
    [Fact]
    public void BuildSql_EqualsMissingRightInputConnectedToWhere_ReturnsWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel equals = Node(NodeType.Equals);
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", equals, "left");
        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");
        Connect(canvas, equals, "result", result, "where");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(equals);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("missing required input 'right'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_BetweenMissingHighInputConnectedToWhere_ReturnsWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel lowValue = Node(NodeType.ValueNumber);
        lowValue.Parameters["value"] = "1";

        NodeViewModel between = Node(NodeType.Between);
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", between, "value");
        Connect(canvas, lowValue, "result", between, "low");
        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");
        Connect(canvas, between, "result", result, "where");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(lowValue);
        canvas.Nodes.Add(between);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("missing required input 'high'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_LikeWithoutPatternConnectedToWhere_ReturnsWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "name");
        NodeViewModel like = Node(NodeType.Like);
        like.Parameters["pattern"] = "";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "name", like, "text");
        Connect(canvas, orders, "name", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");
        Connect(canvas, like, "result", result, "where");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(like);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("LIKE node connected to WHERE/HAVING/QUALIFY has empty pattern", StringComparison.OrdinalIgnoreCase));
    }

    private static NodeViewModel Node(NodeType type) =>
        new(NodeDefinitionRegistry.Get(type), new Point(0, 0));

    private static NodeViewModel Table(string tableName, params string[] columns) =>
        new(tableName, columns.Select(c => (c, c.Equals("name", StringComparison.OrdinalIgnoreCase) ? PinDataType.Text : PinDataType.Number)), new Point(0, 0));

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
