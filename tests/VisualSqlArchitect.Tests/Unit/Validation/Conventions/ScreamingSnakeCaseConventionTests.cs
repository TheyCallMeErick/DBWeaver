using DBWeaver.UI.ViewModels.Validation.Conventions.Implementations;

namespace DBWeaver.Tests.Unit.Validation.Conventions;

public class ScreamingSnakeCaseConventionTests
{
    private readonly ScreamingSnakeCaseConvention _sut = new();

    [Fact]
    public void ConventionName_ReturnsScreamingSnakeCase() =>
        Assert.Equal("SCREAMING_SNAKE_CASE", _sut.ConventionName);

    [Theory]
    [InlineData("ORDER_TOTAL")]
    [InlineData("A")]
    [InlineData("X1")]
    public void Check_ValidAlias_ReturnsNoViolations(string alias) => Assert.Empty(_sut.Check(alias));

    [Fact]
    public void Check_InvalidAlias_ReturnsScreamingSnakeCaseViolation()
    {
        var violations = _sut.Check("order_total");
        Assert.Contains(violations, v => v.Code == "NAMING_SCREAMING_SNAKE_CASE");
    }

    [Theory]
    [InlineData("order_total", "ORDER_TOTAL")]
    [InlineData("OrderTotal", "ORDER_TOTAL")]
    [InlineData("123name", "_123NAME")]
    public void Normalize_ConvertsAsExpected(string input, string expected) =>
        Assert.Equal(expected, _sut.Normalize(input));
}

