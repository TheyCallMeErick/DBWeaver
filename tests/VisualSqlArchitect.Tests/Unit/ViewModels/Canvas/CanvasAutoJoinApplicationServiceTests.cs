using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Metadata;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class CanvasAutoJoinApplicationServiceTests
{
    [Fact]
    public void TryApplySuggestion_CreatesJoinNodeAndConnections()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        canvas.SpawnTableNode(
            "public.orders",
            [("id", PinDataType.Number), ("customer_id", PinDataType.Number)],
            new Point(40, 40));
        canvas.SpawnTableNode(
            "public.customers",
            [("id", PinDataType.Number)],
            new Point(360, 100));

        var service = new CanvasAutoJoinApplicationService();
        var suggestion = new JoinSuggestion(
            ExistingTable: "public.orders",
            NewTable: "public.customers",
            JoinType: "LEFT",
            LeftColumn: "public.orders.customer_id",
            RightColumn: "public.customers.id",
            OnClause: "public.orders.customer_id = public.customers.id",
            Score: 0.99,
            Confidence: JoinConfidence.CatalogDefinedFk,
            Rationale: "test");

        bool created = service.TryApplySuggestion(
            suggestion,
            canvas.Nodes,
            canvas.Connections,
            canvas.SpawnNode,
            canvas.ConnectPins);

        Assert.True(created);
        Assert.Single(canvas.Nodes, n => n.Type == NodeType.Join);
        Assert.Equal(2, canvas.Connections.Count(c => c.ToPin?.Owner?.Type == NodeType.Join));
    }

    [Fact]
    public void TryApplySuggestion_DoesNotCreateDuplicateJoinForSamePair()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        canvas.SpawnTableNode(
            "public.orders",
            [("id", PinDataType.Number), ("customer_id", PinDataType.Number)],
            new Point(40, 40));
        canvas.SpawnTableNode(
            "public.customers",
            [("id", PinDataType.Number)],
            new Point(360, 100));

        var service = new CanvasAutoJoinApplicationService();
        var suggestion = new JoinSuggestion(
            ExistingTable: "public.orders",
            NewTable: "public.customers",
            JoinType: "INNER",
            LeftColumn: "public.orders.customer_id",
            RightColumn: "public.customers.id",
            OnClause: "public.orders.customer_id = public.customers.id",
            Score: 0.99,
            Confidence: JoinConfidence.CatalogDefinedFk,
            Rationale: "test");

        bool first = service.TryApplySuggestion(
            suggestion,
            canvas.Nodes,
            canvas.Connections,
            canvas.SpawnNode,
            canvas.ConnectPins);
        bool second = service.TryApplySuggestion(
            suggestion,
            canvas.Nodes,
            canvas.Connections,
            canvas.SpawnNode,
            canvas.ConnectPins);

        Assert.True(first);
        Assert.False(second);
        Assert.Single(canvas.Nodes, n => n.Type == NodeType.Join);
    }

}


