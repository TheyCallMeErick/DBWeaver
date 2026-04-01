using Avalonia;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.Registry;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.QueryPreview.Services;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.QueryPreview;

public class QueryGraphBuilderWindowFunctionTests
{
    [Fact]
    public void BuildSql_WindowFunctionRowNumber_FromCanvas_EmitsOverClause()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "customer_id", "created_at");
        NodeViewModel rowNumber = Node(NodeType.WindowFunction);
        rowNumber.Parameters["function"] = "RowNumber";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "customer_id", rowNumber, "partition_1");
        Connect(canvas, orders, "created_at", rowNumber, "order_1");
        Connect(canvas, rowNumber, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(rowNumber);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("ROW_NUMBER() OVER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PARTITION BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_WindowFunctionRowNumber_WithSubtractIslandGap_FromCanvas_EmitsArithmetic()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id_processo_refis", "parcela", "created_at");
        NodeViewModel rowNumber = Node(NodeType.WindowFunction);
        rowNumber.Parameters["function"] = "RowNumber";

        NodeViewModel subtract = Node(NodeType.Subtract);
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id_processo_refis", rowNumber, "partition_1");
        Connect(canvas, orders, "created_at", rowNumber, "order_1");
        Connect(canvas, orders, "parcela", subtract, "a");
        Connect(canvas, rowNumber, "result", subtract, "b");
        Connect(canvas, subtract, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(rowNumber);
        canvas.Nodes.Add(subtract);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("ROW_NUMBER() OVER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PARTITION BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("-", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void BuildSql_WindowFunctionRowNumber_WithMultipleOrderPins_HonorsDescendingFlags(DatabaseProvider provider)
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "customer_id", "created_at", "id");
        NodeViewModel rowNumber = Node(NodeType.WindowFunction);
        rowNumber.Parameters["function"] = "RowNumber";
        rowNumber.Parameters["order_1_desc"] = "true";
        rowNumber.AddWindowOrderSlot();

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "customer_id", rowNumber, "partition_1");
        Connect(canvas, orders, "created_at", rowNumber, "order_1");
        Connect(canvas, orders, "id", rowNumber, "order_2");
        Connect(canvas, rowNumber, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(rowNumber);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, provider);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("ROW_NUMBER() OVER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DESC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("created_at", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void BuildSql_WindowFunctionRowNumber_WithFrameClause_EmitsFrame(DatabaseProvider provider)
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "customer_id", "created_at");
        NodeViewModel rowNumber = Node(NodeType.WindowFunction);
        rowNumber.Parameters["function"] = "RowNumber";
        rowNumber.Parameters["frame"] = "UnboundedPreceding_CurrentRow";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "customer_id", rowNumber, "partition_1");
        Connect(canvas, orders, "created_at", rowNumber, "order_1");
        Connect(canvas, rowNumber, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(rowNumber);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, provider);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("ROW_NUMBER() OVER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_WindowFunctionLag_FromCanvas_EmitsLagWithOffsetAndDefault()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "status", "created_at");
        NodeViewModel lag = Node(NodeType.WindowFunction);
        lag.Parameters["function"] = "Lag";
        lag.Parameters["offset"] = "2";
        lag.Parameters["default_value"] = "pending";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "status", lag, "value");
        Connect(canvas, orders, "created_at", lag, "order_1");
        Connect(canvas, lag, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(lag);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("LAG(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OVER", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_WindowFunctionLead_FromCanvas_EmitsLeadWithOffsetAndDefault()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "status", "created_at");
        NodeViewModel lead = Node(NodeType.WindowFunction);
        lead.Parameters["function"] = "Lead";
        lead.Parameters["offset"] = "1";
        lead.Parameters["default_value"] = "pending";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "status", lead, "value");
        Connect(canvas, orders, "created_at", lead, "order_1");
        Connect(canvas, lead, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(lead);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("LEAD(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OVER", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_WindowFunctionFirstValue_FromCanvas_EmitsFirstValue()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "status", "created_at");
        NodeViewModel firstValue = Node(NodeType.WindowFunction);
        firstValue.Parameters["function"] = "FirstValue";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "status", firstValue, "value");
        Connect(canvas, orders, "created_at", firstValue, "order_1");
        Connect(canvas, firstValue, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(firstValue);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("FIRST_VALUE(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OVER", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_WindowFunctionLastValue_WithFrame_FromCanvas_EmitsLastValueAndFrame()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "status", "created_at");
        NodeViewModel lastValue = Node(NodeType.WindowFunction);
        lastValue.Parameters["function"] = "LastValue";
        lastValue.Parameters["frame"] = "CurrentRow_UnboundedFollowing";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "status", lastValue, "value");
        Connect(canvas, orders, "created_at", lastValue, "order_1");
        Connect(canvas, lastValue, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(lastValue);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("LAST_VALUE(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OVER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROWS BETWEEN CURRENT ROW AND UNBOUNDED FOLLOWING", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void BuildSql_WindowFunctionRowNumber_WithFrameNone_DoesNotEmitRowsClause(DatabaseProvider provider)
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "customer_id", "created_at");
        NodeViewModel rowNumber = Node(NodeType.WindowFunction);
        rowNumber.Parameters["function"] = "RowNumber";
        rowNumber.Parameters["frame"] = "None";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "customer_id", rowNumber, "partition_1");
        Connect(canvas, orders, "created_at", rowNumber, "order_1");
        Connect(canvas, rowNumber, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(rowNumber);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, provider);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("ROW_NUMBER() OVER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ROWS BETWEEN", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("null", "NULL")]
    [InlineData("42", "42")]
    [InlineData("true", "TRUE")]
    public void BuildSql_WindowFunctionLag_WithTypedDefault_FromCanvas_EmitsTypedLiteral(string defaultValue, string expectedToken)
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "status", "created_at");
        NodeViewModel lag = Node(NodeType.WindowFunction);
        lag.Parameters["function"] = "Lag";
        lag.Parameters["offset"] = "1";
        lag.Parameters["default_value"] = defaultValue;

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "status", lag, "value");
        Connect(canvas, orders, "created_at", lag, "order_1");
        Connect(canvas, lag, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(lag);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("LAG(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedToken, sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("null", "NULL")]
    [InlineData("42", "42")]
    [InlineData("false", "FALSE")]
    public void BuildSql_WindowFunctionLead_WithTypedDefault_FromCanvas_EmitsTypedLiteral(string defaultValue, string expectedToken)
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "status", "created_at");
        NodeViewModel lead = Node(NodeType.WindowFunction);
        lead.Parameters["function"] = "Lead";
        lead.Parameters["offset"] = "1";
        lead.Parameters["default_value"] = defaultValue;

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "status", lead, "value");
        Connect(canvas, orders, "created_at", lead, "order_1");
        Connect(canvas, lead, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(lead);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("LEAD(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedToken, sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("SumOver", "SUM(")]
    [InlineData("AvgOver", "AVG(")]
    [InlineData("MinOver", "MIN(")]
    [InlineData("MaxOver", "MAX(")]
    public void BuildSql_WindowFunctionAggregateOver_WithValue_EmitsAggregateOver(string function, string expectedToken)
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "amount", "created_at");
        NodeViewModel over = Node(NodeType.WindowFunction);
        over.Parameters["function"] = function;

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "amount", over, "value");
        Connect(canvas, orders, "created_at", over, "order_1");
        Connect(canvas, over, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(over);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains(expectedToken, sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OVER", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_WindowFunctionCountOver_WithoutValue_EmitsCountStarOver()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "created_at");
        NodeViewModel countOver = Node(NodeType.WindowFunction);
        countOver.Parameters["function"] = "CountOver";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "created_at", countOver, "order_1");
        Connect(canvas, countOver, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(countOver);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("COUNT(*)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OVER", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_WindowFunctionSumOver_WithoutValue_ReturnsValidationError()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "created_at");
        NodeViewModel sumOver = Node(NodeType.WindowFunction);
        sumOver.Parameters["function"] = "SumOver";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "created_at", sumOver, "order_1");
        Connect(canvas, sumOver, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(sumOver);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("requires a connected 'value' input", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_WindowFunctionRowNumber_WithFrameWithoutOrder_ReturnsFrameWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "customer_id");
        NodeViewModel rowNumber = Node(NodeType.WindowFunction);
        rowNumber.Parameters["function"] = "RowNumber";
        rowNumber.Parameters["frame"] = "UnboundedPreceding_CurrentRow";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "customer_id", rowNumber, "partition_1");
        Connect(canvas, rowNumber, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(rowNumber);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("requires at least one ORDER BY", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("frame clause will be ignored", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_WindowFunction_WithCustomFrame_EmitsCustomRowsBetweenClause()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "customer_id", "created_at");
        NodeViewModel rowNumber = Node(NodeType.WindowFunction);
        rowNumber.Parameters["function"] = "RowNumber";
        rowNumber.Parameters["frame"] = "Custom";
        rowNumber.Parameters["frame_start"] = "Preceding";
        rowNumber.Parameters["frame_start_offset"] = "2";
        rowNumber.Parameters["frame_end"] = "Following";
        rowNumber.Parameters["frame_end_offset"] = "1";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "customer_id", rowNumber, "partition_1");
        Connect(canvas, orders, "created_at", rowNumber, "order_1");
        Connect(canvas, rowNumber, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(rowNumber);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("ROWS BETWEEN 2 PRECEDING AND 1 FOLLOWING", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_WindowFunction_WithCustomFrameInvalidOffsets_ReturnsWarnings()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "customer_id", "created_at");
        NodeViewModel rowNumber = Node(NodeType.WindowFunction);
        rowNumber.Parameters["function"] = "RowNumber";
        rowNumber.Parameters["frame"] = "Custom";
        rowNumber.Parameters["frame_start"] = "Preceding";
        rowNumber.Parameters["frame_start_offset"] = "abc";
        rowNumber.Parameters["frame_end"] = "Following";
        rowNumber.Parameters["frame_end_offset"] = "-1";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "customer_id", rowNumber, "partition_1");
        Connect(canvas, orders, "created_at", rowNumber, "order_1");
        Connect(canvas, rowNumber, "result", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(rowNumber);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("frame_start_offset", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("frame_end_offset", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_WindowFunctionLagAndNtile_WithInvalidNumericParams_ReturnsWarnings()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "status", "created_at");

        NodeViewModel lag = Node(NodeType.WindowFunction);
        lag.Parameters["function"] = "Lag";
        lag.Parameters["offset"] = "0";

        NodeViewModel ntile = Node(NodeType.WindowFunction);
        ntile.Parameters["function"] = "Ntile";
        ntile.Parameters["ntile_groups"] = "-3";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "status", lag, "value");
        Connect(canvas, orders, "created_at", lag, "order_1");
        Connect(canvas, lag, "result", columnList, "columns");

        Connect(canvas, orders, "created_at", ntile, "order_1");

        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(lag);
        canvas.Nodes.Add(ntile);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("invalid offset", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("invalid ntile_groups", StringComparison.OrdinalIgnoreCase));
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
