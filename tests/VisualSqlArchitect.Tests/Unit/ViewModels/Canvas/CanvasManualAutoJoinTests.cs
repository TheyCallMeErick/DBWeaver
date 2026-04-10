using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class CanvasManualAutoJoinTests
{
    [Fact]
    public void RunSelectedAutoJoin_WithMatchingHeuristic_CreatesJoinOrRequestsConfirmationWhenAmbiguous()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = canvas.SpawnTableNode(
            "public.orders",
            [("id", PinDataType.Number), ("customer_id", PinDataType.Number)],
            new Point(80, 80)
        );
        NodeViewModel customers = canvas.SpawnTableNode(
            "public.customers",
            [("id", PinDataType.Number)],
            new Point(320, 120)
        );

        orders.IsSelected = true;
        customers.IsSelected = true;

        Assert.True(canvas.HasTwoSelectedTableSources);

        canvas.RunSelectedAutoJoinCommand.Execute(null);

        if (canvas.ManualJoinDialog.IsVisible)
        {
            canvas.ManualJoinDialog.SelectedLeftColumn = canvas.ManualJoinDialog.LeftColumns
                .First(c => c.Name == "customer_id");
            canvas.ManualJoinDialog.SelectedRightColumn = canvas.ManualJoinDialog.RightColumns
                .First(c => c.Name == "id");
            canvas.ManualJoinDialog.ConfirmCommand.Execute(null);
        }

        NodeViewModel joinNode = Assert.Single(canvas.Nodes, n => n.Type == NodeType.Join);
        Assert.False(string.IsNullOrWhiteSpace(joinNode.Parameters["join_type"]));
        Assert.Equal(2, canvas.Connections.Count(c => c.ToPin?.Owner == joinNode));
    }

    [Fact]
    public void RunSelectedAutoJoin_WhenAutoJoinCannotCreate_DoesFallbackToManualDialog_AndConfirmCreatesJoin()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel left = canvas.SpawnTableNode(
            "public.orders",
            [("customer_id", PinDataType.Number), ("external_customer_id", PinDataType.Number)],
            new Point(50, 40)
        );
        NodeViewModel right = canvas.SpawnTableNode(
            "public.customers",
            [("id", PinDataType.Number)],
            new Point(300, 40)
        );

        // Existing join forces auto-join creation attempt to fail (duplicate),
        // so the command should fallback to manual join dialog.
        NodeViewModel existingJoin = canvas.SpawnNode(
            NodeDefinitionRegistry.Get(NodeType.Join),
            new Point(200, 80)
        );
        canvas.ConnectPins(left.OutputPins[0], existingJoin.InputPins.First(p => p.Name == "left"));
        canvas.ConnectPins(right.OutputPins[0], existingJoin.InputPins.First(p => p.Name == "right"));

        left.IsSelected = true;
        right.IsSelected = true;

        canvas.RunSelectedAutoJoinCommand.Execute(null);

        Assert.True(canvas.ManualJoinDialog.IsVisible);
        canvas.ManualJoinDialog.SelectedLeftColumn = canvas.ManualJoinDialog.LeftColumns
            .First(c => c.Name == "external_customer_id");
        canvas.ManualJoinDialog.SelectedRightColumn = canvas.ManualJoinDialog.RightColumns
            .First(c => c.Name == "id");
        canvas.ManualJoinDialog.SelectedJoinType = "LEFT";
        canvas.ManualJoinDialog.ConfirmCommand.Execute(null);

        Assert.Equal(2, canvas.Nodes.Count(n => n.Type == NodeType.Join));
        NodeViewModel joinNode = canvas.Nodes.Last(n => n.Type == NodeType.Join);
        Assert.Equal("LEFT", joinNode.Parameters["join_type"]);
        Assert.Equal("public.customers", joinNode.Parameters["right_source"]);
        Assert.Equal("public.orders.external_customer_id", joinNode.Parameters["left_expr"]);
        Assert.Equal("public.customers.id", joinNode.Parameters["right_expr"]);
    }
}


