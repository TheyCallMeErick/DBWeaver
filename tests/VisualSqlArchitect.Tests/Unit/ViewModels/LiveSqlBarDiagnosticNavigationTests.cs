using DBWeaver.UI.Services.Benchmark;
using Avalonia;
using DBWeaver.Core;
using DBWeaver.Nodes;
using DBWeaver.Registry;
using DBWeaver.UI.Services.LiveSqlBar;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.Services.QueryPreview.Models;

namespace DBWeaver.Tests.Unit.ViewModels;

public class LiveSqlBarDiagnosticNavigationTests
{
    [Fact]
    public void FocusDiagnosticCommand_SelectsNodeFromDiagnosticContext()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel ordersA = Table("orders", "id");
        NodeViewModel ordersB = Table("orders_archive", "id");
        ordersA.Alias = "o";
        ordersB.Alias = "o";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, ordersA, "id", columnList, "columns");
        Connect(canvas, ordersB, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(ordersA);
        canvas.Nodes.Add(ordersB);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var liveSql = new LiveSqlBarViewModel(canvas);
        liveSql.Recompile();

        PreviewDiagnostic diagnostic = Assert.Single(
            liveSql.Diagnostics,
            d => d.Message.Contains("alias ambiguity", StringComparison.OrdinalIgnoreCase)
                 && !string.IsNullOrWhiteSpace(d.NodeId)
        );

        LiveSqlDiagnosticItem item = Assert.Single(
            liveSql.DiagnosticItems,
            d => d.Message == diagnostic.Message
        );
        item.FocusCommand.Execute(null);

        NodeViewModel selected = Assert.Single(canvas.Nodes, n => n.IsSelected);
        Assert.Equal(diagnostic.NodeId, selected.Id);
    }

    [Fact]
    public void QuickActions_AreAvailable_ForAliasJoinAndComparisonWarnings()
    {
        Assert.Contains(
            BuildAliasAmbiguityLiveSql().DiagnosticItems,
            d => d.QuickActionLabel == "Definir aliases distintos"
        );
        Assert.Contains(
            BuildJoinWarningLiveSql().DiagnosticItems,
            d => d.QuickActionLabel == "Revisar JOIN"
        );
        Assert.Contains(
            BuildComparisonWarningLiveSql().DiagnosticItems,
            d => d.QuickActionLabel == "Abrir propriedade pattern"
        );
    }

    [Fact]
    public void QuickActionCommand_IsSafe_AndDoesNotMutateGraph()
    {
        LiveSqlBarViewModel liveSql = BuildJoinWarningLiveSql();
        int diagnosticsBefore = liveSql.Diagnostics.Count;
        int itemsBefore = liveSql.DiagnosticItems.Count;
        string sqlBefore = liveSql.RawSql;

        LiveSqlDiagnosticItem actionItem = Assert.Single(
            liveSql.DiagnosticItems,
            d => d.QuickActionLabel == "Revisar JOIN"
        );

        Assert.True(actionItem.QuickActionCommand.CanExecute(null));
        actionItem.QuickActionCommand.Execute(null);

        Assert.Equal(diagnosticsBefore, liveSql.Diagnostics.Count);
        Assert.Equal(itemsBefore, liveSql.DiagnosticItems.Count);
        Assert.Equal(sqlBefore, liveSql.RawSql);
    }

    private static LiveSqlBarViewModel BuildAliasAmbiguityLiveSql()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel ordersA = Table("orders", "id");
        NodeViewModel ordersB = Table("orders_archive", "id");
        ordersA.Alias = "o";
        ordersB.Alias = "o";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, ordersA, "id", columnList, "columns");
        Connect(canvas, ordersB, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(ordersA);
        canvas.Nodes.Add(ordersB);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var liveSql = new LiveSqlBarViewModel(canvas);
        liveSql.Recompile();
        return liveSql;
    }

    private static LiveSqlBarViewModel BuildJoinWarningLiveSql()
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

        var liveSql = new LiveSqlBarViewModel(canvas);
        liveSql.Recompile();
        return liveSql;
    }

    private static LiveSqlBarViewModel BuildComparisonWarningLiveSql()
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

        var liveSql = new LiveSqlBarViewModel(canvas);
        liveSql.Recompile();
        return liveSql;
    }

    private static NodeViewModel Node(NodeType type) =>
        new(NodeDefinitionRegistry.Get(type), new Point(0, 0));

    private static NodeViewModel Table(string tableName, params string[] columns) =>
        new(
            tableName,
            columns.Select(c =>
                (
                    c,
                    c.Equals("name", StringComparison.OrdinalIgnoreCase)
                        ? PinDataType.Text
                        : PinDataType.Number
                )
            ),
            new Point(0, 0)
        );

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


