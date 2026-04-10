using Avalonia;
using DBWeaver.Core;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.Services.QueryPreview;
using static DBWeaver.Tests.Unit.ViewModels.QueryPreview.QueryPreviewTestNodeFactory;

namespace DBWeaver.Tests.Unit.ViewModels.QueryPreview;

public class QueryGraphBuilderAliasAmbiguityTests
{
    [Fact]
    public void BuildSql_DuplicateSourceAliasInMainScope_ReturnsAmbiguityWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("public.orders", "id");
        NodeViewModel customers = Table("public.customers", "id");
        orders.Alias = "dup";
        customers.Alias = "dup";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, customers, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(customers);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("Potential alias ambiguity in main query scope", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("alias 'dup'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_SameAliasAcrossMainAndCteScopes_DoesNotReturnAmbiguityWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel mainTable = Table("public.main_orders", "id");
        mainTable.Alias = "dup";

        NodeViewModel mainColumnList = Node(NodeType.ColumnList);
        NodeViewModel mainResult = Node(NodeType.ResultOutput);
        Connect(canvas, mainTable, "id", mainColumnList, "columns");
        Connect(canvas, mainColumnList, "result", mainResult, "columns");

        NodeViewModel cteTable = Table("public.cte_orders", "id");
        cteTable.Alias = "dup";

        NodeViewModel cteColumnList = Node(NodeType.ColumnList);
        NodeViewModel cteResult = Node(NodeType.ResultOutput);
        NodeViewModel cteDef = Node(NodeType.CteDefinition);
        cteDef.Parameters["name"] = "orders_cte";

        Connect(canvas, cteTable, "id", cteColumnList, "columns");
        Connect(canvas, cteColumnList, "result", cteResult, "columns");
        Connect(canvas, cteResult, "result", cteDef, "query");

        canvas.Nodes.Add(mainTable);
        canvas.Nodes.Add(mainColumnList);
        canvas.Nodes.Add(mainResult);
        canvas.Nodes.Add(cteTable);
        canvas.Nodes.Add(cteColumnList);
        canvas.Nodes.Add(cteResult);
        canvas.Nodes.Add(cteDef);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.DoesNotContain(errors, e => e.Contains("Potential alias ambiguity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSql_DuplicateAliasInsideCteScope_ReturnsCteAmbiguityWarning()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel mainTable = Table("public.main_orders", "id");
        NodeViewModel mainColumnList = Node(NodeType.ColumnList);
        NodeViewModel mainResult = Node(NodeType.ResultOutput);
        Connect(canvas, mainTable, "id", mainColumnList, "columns");
        Connect(canvas, mainColumnList, "result", mainResult, "columns");

        NodeViewModel cteTableA = Table("public.cte_orders_a", "id");
        NodeViewModel cteTableB = Table("public.cte_orders_b", "id");
        cteTableA.Alias = "dup_cte";
        cteTableB.Alias = "dup_cte";

        NodeViewModel cteColumnList = Node(NodeType.ColumnList);
        NodeViewModel cteResult = Node(NodeType.ResultOutput);
        NodeViewModel cteDef = Node(NodeType.CteDefinition);
        cteDef.Parameters["name"] = "orders_cte";

        Connect(canvas, cteTableA, "id", cteColumnList, "columns");
        Connect(canvas, cteTableB, "id", cteColumnList, "columns");
        Connect(canvas, cteColumnList, "result", cteResult, "columns");
        Connect(canvas, cteResult, "result", cteDef, "query");

        canvas.Nodes.Add(mainTable);
        canvas.Nodes.Add(mainColumnList);
        canvas.Nodes.Add(mainResult);
        canvas.Nodes.Add(cteTableA);
        canvas.Nodes.Add(cteTableB);
        canvas.Nodes.Add(cteColumnList);
        canvas.Nodes.Add(cteResult);
        canvas.Nodes.Add(cteDef);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string _, List<string> errors) = sut.BuildSql();

        Assert.Contains(errors, e => e.Contains("Potential alias ambiguity in CTE 'orders_cte' scope", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("alias 'dup_cte'", StringComparison.OrdinalIgnoreCase));
    }

}



