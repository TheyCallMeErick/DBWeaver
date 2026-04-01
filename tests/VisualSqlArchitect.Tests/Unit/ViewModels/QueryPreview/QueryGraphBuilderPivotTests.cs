using Avalonia;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.Registry;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.QueryPreview.Services;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.QueryPreview;

public class QueryGraphBuilderPivotTests
{
    [Fact]
    public void BuildSql_SqlServerPivot_WrapsQueryWithPivotClause()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel sales = Table("dbo.Sales", "Region", "Amount", "Month");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["pivot_mode"] = "PIVOT";
        result.Parameters["pivot_config"] = "SUM(Amount) FOR Month IN ([Jan],[Feb])";

        Connect(canvas, sales, "Region", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(sales);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.SqlServer);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("PIVOT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SUM(Amount)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IN ([Jan],[Feb])", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_SqlServerUnpivot_WrapsQueryWithUnpivotClause()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel sales = Table("dbo.Sales", "Region", "Jan", "Feb");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["pivot_mode"] = "UNPIVOT";
        result.Parameters["pivot_config"] = "Amount FOR Month IN ([Jan],[Feb])";

        Connect(canvas, sales, "Region", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(sales);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.SqlServer);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("UNPIVOT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Amount FOR Month", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_PostgresPivot_ReturnsWarningAndSkipsPivotClause()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel sales = Table("public.sales", "region");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["pivot_mode"] = "PIVOT";
        result.Parameters["pivot_config"] = "SUM(amount) FOR month IN ('Jan','Feb')";

        Connect(canvas, sales, "region", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(sales);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("PIVOT/UNPIVOT", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("PIVOT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UNPIVOT", sql, StringComparison.OrdinalIgnoreCase);
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
