using DBWeaver.CanvasKit;

namespace DBWeaver.Tests.Unit.CanvasLib;

public sealed class CanvasAutoJoinSemanticsTests
{
    [Fact]
    public void BuildSuggestionPairKey_IsOrderIndependentForTables()
    {
        string a = CanvasAutoJoinSemantics.BuildSuggestionPairKey("public.orders", "public.customers", "orders.customer_id", "customers.id");
        string b = CanvasAutoJoinSemantics.BuildSuggestionPairKey("public.customers", "public.orders", "orders.customer_id", "customers.id");

        Assert.Equal(a, b);
    }

    [Fact]
    public void TryParseQualifiedColumn_ReturnsSourceAndColumn_WhenValidExpression()
    {
        bool ok = CanvasAutoJoinSemantics.TryParseQualifiedColumn("public.orders.customer_id", out string? source, out string column);

        Assert.True(ok);
        Assert.Equal("public.orders", source);
        Assert.Equal("customer_id", column);
    }

    [Fact]
    public void TryParseQualifiedColumn_StripsSqlQuoting()
    {
        bool ok = CanvasAutoJoinSemantics.TryParseQualifiedColumn("[public.orders].[customer_id]", out string? source, out string column);

        Assert.True(ok);
        Assert.Equal("public.orders", source);
        Assert.Equal("customer_id", column);
    }

    [Fact]
    public void TryParseQualifiedColumn_ReturnsFalse_WhenInvalid()
    {
        Assert.False(CanvasAutoJoinSemantics.TryParseQualifiedColumn("invalid", out _, out _));
        Assert.False(CanvasAutoJoinSemantics.TryParseQualifiedColumn("", out _, out _));
    }

    [Fact]
    public void MatchesSource_MatchesByAliasShortAndFullName()
    {
        Assert.True(CanvasAutoJoinSemantics.MatchesSource("public.orders", "orders", "o", "o"));
        Assert.True(CanvasAutoJoinSemantics.MatchesSource("public.orders", "orders", "o", "orders"));
        Assert.True(CanvasAutoJoinSemantics.MatchesSource("public.orders", "orders", "o", "public.orders"));
    }

    [Fact]
    public void MatchesSource_ReturnsFalse_WhenUnrelated()
    {
        Assert.False(CanvasAutoJoinSemantics.MatchesSource("public.orders", "orders", "o", "customers"));
    }
}
