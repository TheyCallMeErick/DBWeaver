using DBWeaver.UI.ViewModels.Validation.Conventions.Implementations;

namespace DBWeaver.Tests.Unit.Validation.Conventions;

public class PascalCaseConventionTests
{
    private readonly PascalCaseConvention _sut = new();

    [Fact]
    public void ConventionName_ReturnsPascalCase() => Assert.Equal("PascalCase", _sut.ConventionName);

    [Theory]
    [InlineData("OrderTotal")]
    [InlineData("Alias")]
    [InlineData("A1")]
    public void Check_ValidAlias_ReturnsNoViolations(string alias) => Assert.Empty(_sut.Check(alias));

    [Fact]
    public void Check_InvalidAlias_ReturnsPascalCaseViolation()
    {
        var violations = _sut.Check("order_total");
        Assert.Contains(violations, v => v.Code == "NAMING_PASCAL_CASE");
    }

    [Theory]
    [InlineData("order_total", "OrderTotal")]
    [InlineData("orderTotal", "OrderTotal")]
    [InlineData("123_name", "A123Name")]
    public void Normalize_ConvertsAsExpected(string input, string expected) =>
        Assert.Equal(expected, _sut.Normalize(input));
}

