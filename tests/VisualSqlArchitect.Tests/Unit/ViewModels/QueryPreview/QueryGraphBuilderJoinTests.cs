using Avalonia;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.QueryPreview.Services;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.QueryPreview;

public class QueryGraphBuilderJoinTests
{
    [Fact]
    public void BuildSql_ExplicitJoinNode_UsesJoinTypeParameterWhenProvided()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id", "customer_id");
        NodeViewModel customers = Table("public.customers", "id");
        NodeViewModel join = Node(NodeType.Join);
        join.Parameters["join_type"] = "RIGHT";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "customer_id", join, "left");
        Connect(canvas, customers, "id", join, "right");
        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(customers);
        canvas.Nodes.Add(join);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("right join", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_ExplicitJoinNode_UsesConfiguredJoinType()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id", "customer_id");
        NodeViewModel customers = Table("public.customers", "id");
        NodeViewModel join = Node(NodeType.Join);
        join.Parameters["join_type"] = "LEFT";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "customer_id", join, "left");
        Connect(canvas, customers, "id", join, "right");
        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(customers);
        canvas.Nodes.Add(join);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("left join", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customers", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orders", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_WithoutExplicitJoinNode_UsesLegacyTableToTableInference()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id", "customer_id");
        NodeViewModel customers = Table("public.customers", "id");

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "customer_id", customers, "id");
        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(customers);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("left join", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customers", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_ExplicitJoinNode_WithFullJoin_UsesFullJoinClause()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id", "customer_id");
        NodeViewModel customers = Table("public.customers", "id");
        NodeViewModel join = Node(NodeType.Join);
        join.Parameters["join_type"] = "FULL";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "customer_id", join, "left");
        Connect(canvas, customers, "id", join, "right");
        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(customers);
        canvas.Nodes.Add(join);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("full join", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_ExplicitJoinNodeMissingSide_ReturnsWarningAndSkipsExplicitJoin()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id", "customer_id");
        NodeViewModel customers = Table("public.customers", "id");
        NodeViewModel join = Node(NodeType.Join);
        join.Parameters["join_type"] = "LEFT";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "customer_id", join, "left");
        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(customers);
        canvas.Nodes.Add(join);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("Join node is incomplete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("join", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_ExplicitJoinNode_WithConditionPin_UsesComparisonNode()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id", "customer_id");
        NodeViewModel customers = Table("public.customers", "id");
        NodeViewModel join = Node(NodeType.Join);
        join.Parameters["join_type"] = "INNER";
        NodeViewModel equals = Node(NodeType.Equals);

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "customer_id", equals, "left");
        Connect(canvas, customers, "id", equals, "right");
        Connect(canvas, equals, "result", join, "condition");
        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(customers);
        canvas.Nodes.Add(join);
        canvas.Nodes.Add(equals);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("join", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orders", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customers", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_ExplicitJoinNode_WithConditionPinAndGreaterThan_UsesComparisonOperator()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id", "customer_id");
        NodeViewModel customers = Table("public.customers", "id");
        NodeViewModel join = Node(NodeType.Join);
        join.Parameters["join_type"] = "INNER";
        NodeViewModel greaterThan = Node(NodeType.GreaterThan);

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "customer_id", greaterThan, "left");
        Connect(canvas, customers, "id", greaterThan, "right");
        Connect(canvas, greaterThan, "result", join, "condition");
        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(customers);
        canvas.Nodes.Add(join);
        canvas.Nodes.Add(greaterThan);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains(">", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_ExplicitJoinNode_WithExpressionParameters_UsesConfiguredRightSource()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id", "customer_id");
        NodeViewModel join = Node(NodeType.Join);
        join.Parameters["join_type"] = "LEFT";
        join.Parameters["operator"] = "=";
        join.Parameters["right_source"] = "cte_customers c";
        join.Parameters["left_expr"] = "public.orders.customer_id";
        join.Parameters["right_expr"] = "c.id";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(join);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("left join", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cte_customers c", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customer_id", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_ExplicitJoinNode_WithArbitraryConditionNode_UsesRawOnClause()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id", "customer_id");
        NodeViewModel customers = Table("public.customers", "id", "customer_id");

        NodeViewModel eq1 = Node(NodeType.Equals);
        NodeViewModel eq2 = Node(NodeType.Equals);
        NodeViewModel and = Node(NodeType.And);

        NodeViewModel join = Node(NodeType.Join);
        join.Parameters["join_type"] = "INNER";
        join.Parameters["right_source"] = "public.customers c";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "customer_id", eq1, "left");
        Connect(canvas, customers, "id", eq1, "right");
        Connect(canvas, orders, "id", eq2, "left");
        Connect(canvas, customers, "customer_id", eq2, "right");

        Connect(canvas, eq1, "result", and, "conditions");
        Connect(canvas, eq2, "result", and, "conditions");
        Connect(canvas, and, "result", join, "condition");

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(customers);
        canvas.Nodes.Add(eq1);
        canvas.Nodes.Add(eq2);
        canvas.Nodes.Add(and);
        canvas.Nodes.Add(join);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("join", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" and ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customer_id", sql, StringComparison.OrdinalIgnoreCase);
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
        PinViewModel to = toNode.InputPins.FirstOrDefault(p => p.Name == toPin)
            ?? toNode.OutputPins.First(p => p.Name == toPin);

        canvas.Connections.Add(new ConnectionViewModel(from, from.AbsolutePosition, to.AbsolutePosition)
        {
            ToPin = to,
        });
    }
}
