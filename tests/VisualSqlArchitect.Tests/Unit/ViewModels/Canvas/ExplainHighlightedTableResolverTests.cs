using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainHighlightedTableResolverTests
{
    private static readonly IExplainHighlightedTableResolver Sut = new ExplainHighlightedTableResolver();

    [Fact]
    public void Resolve_ReturnsTable_FromDetailRelation()
    {
        var step = new ExplainStep
        {
            Operation = "Seq Scan",
            Detail = "relation=public.orders | filter=(status = 'open')",
        };

        string? table = Sut.Resolve(step);

        Assert.Equal("public.orders", table);
    }

    [Fact]
    public void Resolve_FallsBackToOperationOnPattern()
    {
        var step = new ExplainStep
        {
            Operation = "Index Scan using idx_orders on orders",
            Detail = null,
        };

        string? table = Sut.Resolve(step);

        Assert.Equal("orders", table);
    }

    [Fact]
    public void Resolve_NormalizesQuotedIdentifiers()
    {
        var step = new ExplainStep
        {
            Operation = "Seq Scan",
            Detail = "relation=\"public\".\"Customers\"",
        };

        string? table = Sut.Resolve(step);

        Assert.Equal("public.Customers", table);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenNoTableReferenceExists()
    {
        var step = new ExplainStep
        {
            Operation = "Hash Join",
            Detail = "hashCond=(a.id = b.id)",
        };

        string? table = Sut.Resolve(step);

        Assert.Null(table);
    }
}

