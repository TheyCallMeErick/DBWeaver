using DBWeaver.CanvasKit;

namespace DBWeaver.Tests.Unit.CanvasLib;

public sealed class CanvasTableHighlightEngineTests
{
    [Fact]
    public void NormalizeTableReference_RemovesDecoratorsAndAliasSuffix()
    {
        string? normalized = CanvasTableHighlightEngine.NormalizeTableReference(" [public].[Orders] o ");

        Assert.Equal("public.Orders", normalized);
    }

    [Fact]
    public void ApplyHighlight_HighlightsMatchingNodeByFullName()
    {
        var nodes = new List<FakeTableNode>
        {
            new() { IsTableSource = true, FullName = "public.orders", Title = "orders" },
            new() { IsTableSource = true, FullName = "public.customers", Title = "customers" },
        };

        CanvasTableHighlightEngine.ApplyHighlight(nodes, "public.orders");

        Assert.True(nodes[0].IsHighlighted);
        Assert.False(nodes[1].IsHighlighted);
    }

    [Fact]
    public void ApplyHighlight_MatchesByAliasWhenAvailable()
    {
        var node = new FakeTableNode
        {
            IsTableSource = true,
            FullName = "public.order_items",
            Title = "order_items",
            Alias = "oi",
        };

        CanvasTableHighlightEngine.ApplyHighlight([node], "oi");

        Assert.True(node.IsHighlighted);
    }

    [Fact]
    public void ApplyHighlight_ClearsHighlightsForNonTableSourceNodes()
    {
        var node = new FakeTableNode
        {
            IsTableSource = false,
            FullName = "public.orders",
            Title = "orders",
            IsHighlighted = true,
        };

        CanvasTableHighlightEngine.ApplyHighlight([node], "public.orders");

        Assert.False(node.IsHighlighted);
    }

    private sealed class FakeTableNode : ICanvasTableNode
    {
        public bool IsTableSource { get; set; }
        public bool IsHighlighted { get; set; }
        public string? FullName { get; set; }
        public string? Title { get; set; }
        public string? Alias { get; set; }
    }
}
