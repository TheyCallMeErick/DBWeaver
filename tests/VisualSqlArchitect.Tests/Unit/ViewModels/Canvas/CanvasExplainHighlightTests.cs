using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class CanvasExplainHighlightTests
{
    [Fact]
    public void SelectStep_HighlightsMatchingTableNode()
    {
        var canvas = new CanvasViewModel();
        NodeViewModel orders = new("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        NodeViewModel customers = new("public.customers", [("id", PinDataType.Number)], new Point(280, 0));
        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(customers);

        canvas.ExplainPlan.SelectStepCommand.Execute(new ExplainStep
        {
            Operation = "Seq Scan",
            Detail = "relation=orders",
        });

        Assert.Equal("orders", canvas.ExplainPlan.HighlightedTableName);
        Assert.True(orders.IsHighlighted);
        Assert.False(customers.IsHighlighted);
    }

    [Fact]
    public void CloseExplain_ClearsAllNodeHighlights()
    {
        var canvas = new CanvasViewModel();
        NodeViewModel orders = new("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        canvas.Nodes.Add(orders);

        canvas.ExplainPlan.SelectStepCommand.Execute(new ExplainStep
        {
            Operation = "Seq Scan",
            Detail = "relation=orders",
        });
        Assert.True(orders.IsHighlighted);

        canvas.ExplainPlan.Close();

        Assert.Null(canvas.ExplainPlan.HighlightedTableName);
        Assert.False(orders.IsHighlighted);
    }

    [Fact]
    public void Highlight_MatchesNodeAlias_WhenPlanReferencesAlias()
    {
        var canvas = new CanvasViewModel();
        NodeViewModel orders = new("public.orders", [("id", PinDataType.Number)], new Point(0, 0))
        {
            Alias = "o",
        };
        canvas.Nodes.Add(orders);

        canvas.ExplainPlan.SelectStepCommand.Execute(new ExplainStep
        {
            Operation = "Seq Scan",
            Detail = "relation=o",
        });

        Assert.Equal("o", canvas.ExplainPlan.HighlightedTableName);
        Assert.True(orders.IsHighlighted);
    }

    [Fact]
    public void Highlight_ClearsAndIgnoresNonTableNodes_WhenStepHasNoTable()
    {
        var canvas = new CanvasViewModel();
        NodeViewModel orders = new("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        NodeViewModel join = new(NodeDefinitionRegistry.Get(NodeType.Join), new Point(320, 0))
        {
            IsHighlighted = true,
        };
        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(join);

        canvas.ExplainPlan.SelectStepCommand.Execute(new ExplainStep
        {
            Operation = "Seq Scan",
            Detail = "relation=orders",
        });
        Assert.True(orders.IsHighlighted);

        canvas.ExplainPlan.SelectStepCommand.Execute(new ExplainStep
        {
            Operation = "Hash Join",
            Detail = "hashCond=(a.id = b.id)",
        });

        Assert.Null(canvas.ExplainPlan.HighlightedTableName);
        Assert.False(orders.IsHighlighted);
        Assert.False(join.IsHighlighted);
    }

    [Fact]
    public void Highlight_AppliesToNewlyAddedNode_WhenHighlightAlreadyActive()
    {
        var canvas = new CanvasViewModel();

        canvas.ExplainPlan.SelectStepCommand.Execute(new ExplainStep
        {
            Operation = "Seq Scan",
            Detail = "relation=\"public\".\"orders\"",
        });
        Assert.Equal("public.orders", canvas.ExplainPlan.HighlightedTableName);

        NodeViewModel orders = new("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        canvas.Nodes.Add(orders);

        Assert.True(orders.IsHighlighted);
    }
}


