using DBWeaver.Core;
using DBWeaver.UI.Services.SqlEditor;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlSymbolTableBuilderTests
{
    [Fact]
    public void Build_WithCte_TracksCteNameAndBinding()
    {
        var sut = new SqlSymbolTableBuilder();
        const string sql = "WITH recent_orders AS (SELECT * FROM public.orders) SELECT * FROM recent_orders ro";

        SqlSymbolTable table = sut.Build(sql, DatabaseProvider.Postgres);

        Assert.Contains("recent_orders", table.CteNames, StringComparer.OrdinalIgnoreCase);
        Assert.True(table.TryResolveBinding("ro", out SqlTableBindingSymbol? binding));
        Assert.NotNull(binding);
        Assert.True(binding!.IsCte);
        Assert.Equal("recent_orders", binding.TableRef);
    }

    [Fact]
    public void Build_WithInlineSubquery_TracksSubqueryAlias()
    {
        var sut = new SqlSymbolTableBuilder();
        const string sql = "SELECT * FROM (SELECT id FROM public.orders) o WHERE o.";

        SqlSymbolTable table = sut.Build(sql, DatabaseProvider.Postgres);
        Assert.Contains("o", table.BindingsInOrder.Select(static b => b.Alias), StringComparer.OrdinalIgnoreCase);
        Assert.True(table.TryResolveBinding("o", out SqlTableBindingSymbol? binding));
        Assert.NotNull(binding);
        Assert.True(binding!.IsSubquery);
        Assert.StartsWith("__subquery_", binding.TableRef, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithSqlServerBracketedSubqueryAlias_UsesProviderFallback()
    {
        var sut = new SqlSymbolTableBuilder();
        const string sql = "SELECT * FROM (SELECT id FROM dbo.orders) [ord] WHERE ord.";

        SqlSymbolTable table = sut.Build(sql, DatabaseProvider.SqlServer);

        Assert.True(table.TryResolveBinding("ord", out SqlTableBindingSymbol? binding));
        Assert.NotNull(binding);
        Assert.True(binding!.IsSubquery);
    }

    [Fact]
    public void Build_WithMySqlBacktickSubqueryAlias_UsesProviderFallback()
    {
        var sut = new SqlSymbolTableBuilder();
        const string sql = "SELECT * FROM (SELECT id FROM orders) `ord` WHERE ord.";

        SqlSymbolTable table = sut.Build(sql, DatabaseProvider.MySql);

        Assert.True(table.TryResolveBinding("ord", out SqlTableBindingSymbol? binding));
        Assert.NotNull(binding);
        Assert.True(binding!.IsSubquery);
    }
}
