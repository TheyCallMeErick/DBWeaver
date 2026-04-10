
using Avalonia;
using DBWeaver.Core;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.Services.QueryPreview;
using static DBWeaver.Tests.Unit.ViewModels.QueryPreview.QueryPreviewTestNodeFactory;

namespace DBWeaver.Tests.Unit.ViewModels.QueryPreview;
public class QueryGraphBuilderNotAndJsonValidationTests
{
    [Fact]
    public void BuildSql_NotWithoutConditionConnectedToWhere_ReturnsWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", ("id", PinDataType.Number));
        NodeViewModel notNode = Node(NodeType.Not);
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");
        Connect(canvas, notNode, "result", result, "where");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(notNode);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("NOT node connected to WHERE/HAVING/QUALIFY", StringComparison.OrdinalIgnoreCase)
            && e.Contains("missing required input 'condition'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_JsonExtractWithoutJsonConnectedToSelect_ReturnsWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", ("id", PinDataType.Number));
        NodeViewModel jsonExtract = Node(NodeType.JsonExtract);
        jsonExtract.Parameters["path"] = "$.address.city";
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, jsonExtract, "value", result, "column");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(jsonExtract);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("JSON Extract node connected to WHERE/HAVING/QUALIFY/SELECT is missing required input 'json'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_JsonExtractInvalidPathConnectedToSelect_ReturnsWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", ("payload", PinDataType.Json));
        NodeViewModel jsonExtract = Node(NodeType.JsonExtract);
        jsonExtract.Parameters["path"] = "address.city";
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "payload", jsonExtract, "json");
        Connect(canvas, jsonExtract, "value", result, "column");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(jsonExtract);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("JSON Extract node connected to WHERE/HAVING/QUALIFY/SELECT has invalid 'path'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_JsonArrayLengthWithoutJsonConnectedToSelect_ReturnsWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", ("id", PinDataType.Number));
        NodeViewModel jsonLength = Node(NodeType.JsonArrayLength);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, jsonLength, "length", result, "column");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(jsonLength);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("JSON Array Length node connected to WHERE/HAVING/QUALIFY/SELECT is missing required input 'json'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_JsonExtractInvalidPathNotActive_DoesNotWarn()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", ("id", PinDataType.Number), ("payload", PinDataType.Json));
        NodeViewModel jsonExtract = Node(NodeType.JsonExtract);
        jsonExtract.Parameters["path"] = "invalid.path";
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(jsonExtract);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.DoesNotContain(errors, e => e.Contains("JSON Extract node connected to WHERE/HAVING/QUALIFY/SELECT has invalid 'path'", StringComparison.OrdinalIgnoreCase));
    }
}




