using VisualSqlArchitect.Core;
using VisualSqlArchitect.QueryEngine;

namespace VisualSqlArchitect.Tests.Unit.QueryEngine;

public class FunctionFragmentProviderTests
{
    [Theory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void Regex_GeneratesProviderSpecificSyntax(DatabaseProvider provider)
    {
        var provider_impl = CreateFragmentProvider(provider);
        string fragment = provider_impl.Regex("email", "'@corp'");
        Assert.NotEmpty(fragment);
    }

    [Theory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void JsonExtract_ProducesNonEmptyFragment(DatabaseProvider provider)
    {
        var provider_impl = CreateFragmentProvider(provider);
        string fragment = provider_impl.JsonExtract("data", "'$.key'");
        Assert.NotEmpty(fragment);
    }

    [Theory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.MySql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void DateDiff_ProducesNonEmptyFragment(DatabaseProvider provider)
    {
        var provider_impl = CreateFragmentProvider(provider);
        string fragment = provider_impl.DateDiff("'2024-01-01'", "'2024-12-31'", "'day'");
        Assert.NotEmpty(fragment);
    }

    [Fact]
    public void PostgresFunctionFragments_SpecificBehaviors()
    {
        var fragments = new PostgresFunctionFragments();
        string regex = fragments.Regex("col", "'pattern'");
        Assert.Contains("~", regex);
    }

    [Fact]
    public void MySqlFunctionFragments_SpecificBehaviors()
    {
        var fragments = new MySqlFunctionFragments();
        string regex = fragments.Regex("col", "'pattern'");
        Assert.Contains("REGEXP", regex);
    }

    [Fact]
    public void SqlServerFunctionFragments_SpecificBehaviors()
    {
        var fragments = new SqlServerFunctionFragments();
        string regex = fragments.Regex("col", "'pattern'");
        Assert.Contains("PATINDEX", regex);
    }

    private static IFunctionFragmentProvider CreateFragmentProvider(DatabaseProvider provider) =>
        provider switch
        {
            DatabaseProvider.Postgres => new PostgresFunctionFragments(),
            DatabaseProvider.MySql => new MySqlFunctionFragments(),
            DatabaseProvider.SqlServer => new SqlServerFunctionFragments(),
            _ => throw new NotSupportedException("Provider not supported")
        };
}
