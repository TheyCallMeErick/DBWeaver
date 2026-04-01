using Avalonia;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.Registry;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.QueryPreview.Services;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.QueryPreview;

public class QueryGraphBuilderQueryHintsTests
{
    [Fact]
    public void BuildSql_SqlServerWithQueryHints_AppendsOptionClause()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("dbo.Orders", "Id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["query_hints"] = "TABLE HINT(Orders, NOLOCK), RECOMPILE";

        Connect(canvas, orders, "Id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.SqlServer);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("OPTION (", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOLOCK", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RECOMPILE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_PostgresWithQueryHints_EmitsSelectCommentHint()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["query_hints"] = "SeqScan(orders)";

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("/*+", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SeqScan(orders)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OPTION (", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_MySqlWithQueryHints_EmitsSelectCommentHint()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["query_hints"] = "MAX_EXECUTION_TIME(1000)";

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.MySql);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("/*+", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MAX_EXECUTION_TIME(1000)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_SqliteWithQueryHints_ReturnsWarningAndSkipsHint()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["query_hints"] = "ANY_HINT";

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.SQLite);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("not supported", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("/*+", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OPTION (", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_SqlServerWithInvalidHint_ReturnsWarningAndSkipsOptionClause()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("dbo.Orders", "Id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["query_hints"] = "DROP TABLE x";

        Connect(canvas, orders, "Id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.SqlServer);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("unsupported SQL Server OPTION", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("OPTION (", sql, StringComparison.OrdinalIgnoreCase);
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
