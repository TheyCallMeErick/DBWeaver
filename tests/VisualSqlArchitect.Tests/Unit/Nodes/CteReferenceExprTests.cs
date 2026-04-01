using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.Registry;

namespace VisualSqlArchitect.Tests.Unit.Nodes;

public class CteReferenceExprTests
{
    [Theory]
    [InlineData(DatabaseProvider.Postgres, "orders_cte pe")]
    [InlineData(DatabaseProvider.MySql, "orders_cte pe")]
    [InlineData(DatabaseProvider.SqlServer, "orders_cte pe")]
    public void Emit_WithAlias_EmitsCteAndAlias(DatabaseProvider provider, string expected)
    {
        var ctx = new EmitContext(provider, new SqlFunctionRegistry(provider));
        var expr = new CteReferenceExpr("orders_cte", "pe");

        string sql = expr.Emit(ctx);

        Assert.Equal(expected, sql);
    }

    [Fact]
    public void Emit_WithoutAlias_EmitsOnlyCteName()
    {
        var ctx = new EmitContext(DatabaseProvider.Postgres, new SqlFunctionRegistry(DatabaseProvider.Postgres));
        var expr = new CteReferenceExpr("orders_cte");

        string sql = expr.Emit(ctx);

        Assert.Equal("orders_cte", sql);
    }

    [Fact]
    public void Emit_WithWhitespaceAroundNames_TrimsAndEmitsReference()
    {
        var ctx = new EmitContext(DatabaseProvider.Postgres, new SqlFunctionRegistry(DatabaseProvider.Postgres));
        var expr = new CteReferenceExpr("  orders_cte  ", "  pe  ");

        string sql = expr.Emit(ctx);

        Assert.Equal("orders_cte pe", sql);
    }

    [Fact]
    public void Emit_WithWhitespaceAlias_EmitsOnlyCteName()
    {
        var ctx = new EmitContext(DatabaseProvider.Postgres, new SqlFunctionRegistry(DatabaseProvider.Postgres));
        var expr = new CteReferenceExpr("orders_cte", "   ");

        string sql = expr.Emit(ctx);

        Assert.Equal("orders_cte", sql);
    }
}
