using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Validation.Conventions;

namespace DBWeaver.Tests.Unit.Validation.Conventions;

public class NamingConventionValidatorBridgeTests
{
    private readonly IAliasConventionRegistry _registry = AliasConventionRegistry.CreateDefault();

    [Fact]
    public void CheckAlias_WithConventionName_DelegatesToRegistry()
    {
        var policy = new NamingConventionPolicy { ConventionName = "camelCase" };

        var violations = NamingConventionValidator.CheckAlias("OrderTotal", policy, _registry);

        Assert.Single(violations);
        Assert.Equal("NAMING_CAMEL_CASE", violations[0].Code);
    }

    [Fact]
    public void CheckAlias_WithoutConventionName_UsesLegacyPath()
    {
        var policy = new NamingConventionPolicy
        {
            ConventionName = null,
            EnforceSnakeCase = true,
            NoLeadingDigit = true,
            NoSpaces = true,
            MaxLength = 64,
        };

        var violations = NamingConventionValidator.CheckAlias("OrderTotal", policy, _registry);

        Assert.Single(violations);
        Assert.Equal("NAMING_SNAKE_CASE", violations[0].Code);
    }

    [Theory]
    [InlineData("snake_case", "order_total", "order_total")]
    [InlineData("camelCase", "order_total", "orderTotal")]
    [InlineData("PascalCase", "order_total", "OrderTotal")]
    [InlineData("SCREAMING_SNAKE_CASE", "order_total", "ORDER_TOTAL")]
    public void NormalizeAlias_UsesSelectedConvention(string convention, string input, string expected)
    {
        var policy = new NamingConventionPolicy { ConventionName = convention };
        string result = NamingConventionValidator.NormalizeAlias(input, policy, _registry);
        Assert.Equal(expected, result);
    }
}

