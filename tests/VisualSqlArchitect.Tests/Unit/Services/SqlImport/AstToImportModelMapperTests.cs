using VisualSqlArchitect.UI.Services.SqlImport.Mapping;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Services.SqlImport;

public class AstToImportModelMapperTests
{
    [Fact]
    public void Map_WithAliasesAndJoin_MapsCoreClauses()
    {
        var mapper = new AstToImportModelMapper();

        var result = mapper.Map(
            "SELECT o.id AS order_id, c.name FROM public.orders o INNER JOIN public.customers c ON o.customer_id = c.id WHERE o.id > 10 ORDER BY order_id DESC LIMIT 20"
        );

        Assert.True(result.Success);
        Assert.NotNull(result.Model);
        Assert.Empty(result.Issues);

        var model = result.Model!;
        Assert.Equal("public.orders", model.From.Source);
        Assert.Equal("o", model.From.Alias);
        Assert.Single(model.Joins);
        Assert.Equal("public.customers", model.Joins[0].Source);
        Assert.Equal("c", model.Joins[0].Alias);
        Assert.NotNull(model.Where);
        Assert.Single(model.OrderBy);
        Assert.True(model.OrderBy[0].Descending);
        Assert.Equal(20, model.Limit);
    }

    [Fact]
    public void Map_WithGroupByAndHaving_MapsAggregateSections()
    {
        var mapper = new AstToImportModelMapper();

        var result = mapper.Map(
            "SELECT c.name, COUNT(*) AS total FROM public.customers c GROUP BY c.name HAVING COUNT(*) > 1 ORDER BY c.name ASC"
        );

        Assert.True(result.Success);
        Assert.NotNull(result.Model);

        var model = result.Model!;
        Assert.Single(model.GroupBy);
        Assert.NotNull(model.Having);
        Assert.Single(model.OrderBy);
        Assert.False(model.OrderBy[0].Descending);
    }

    [Fact]
    public void Map_WithUnknownAlias_ReturnsSemanticIssue()
    {
        var mapper = new AstToImportModelMapper();

        var result = mapper.Map("SELECT x.id FROM public.orders o");

        Assert.True(result.Success);
        Assert.NotEmpty(result.Issues);
        Assert.Contains(result.Issues, i => i.Code == "UnknownAlias" && i.Context == "x");
    }
}
