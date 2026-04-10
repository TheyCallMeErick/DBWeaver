using DBWeaver.UI.ViewModels.Validation.Conventions.Implementations;

namespace DBWeaver.Tests.Unit.Validation.Conventions;

public class CamelCaseConventionTests
{
    private readonly CamelCaseConvention _sut = new();

    [Fact]
    public void ConventionName_ReturnsCamelCase() => Assert.Equal("camelCase", _sut.ConventionName);

    [Theory]
    [InlineData("orderTotal")]
    [InlineData("a")]
    [InlineData("x1")]
    public void Check_ValidAlias_ReturnsNoViolations(string alias) => Assert.Empty(_sut.Check(alias));

    [Fact]
    public void Check_InvalidAlias_ReturnsCamelCaseViolation()
    {
        var violations = _sut.Check("order_total");
        Assert.Contains(violations, v => v.Code == "NAMING_CAMEL_CASE");
    }

    [Theory]
    [InlineData("order_total", "orderTotal")]
    [InlineData("OrderTotal", "orderTotal")]
    [InlineData("ORDER_TOTAL", "orderTotal")]
    [InlineData("123_order", "a123Order")]
    public void Normalize_ConvertsAsExpected(string input, string expected) =>
        Assert.Equal(expected, _sut.Normalize(input));
}

