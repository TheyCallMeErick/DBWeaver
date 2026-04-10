using DBWeaver.UI.ViewModels.Validation.Conventions.Implementations;

namespace DBWeaver.Tests.Unit.Validation.Conventions;

public class SnakeCaseConventionTests
{
    private readonly SnakeCaseConvention _sut = new();

    [Fact]
    public void ConventionName_ReturnsSnakeCase() => Assert.Equal("snake_case", _sut.ConventionName);

    [Theory]
    [InlineData("order_total")]
    [InlineData("a")]
    [InlineData("abc_123")]
    public void Check_ValidAlias_ReturnsNoViolations(string alias) => Assert.Empty(_sut.Check(alias));

    [Fact]
    public void Check_InvalidAlias_ReturnsSnakeCaseViolation()
    {
        var violations = _sut.Check("OrderTotal");
        Assert.Contains(violations, v => v.Code == "NAMING_SNAKE_CASE");
        Assert.Contains("order_total", violations[0].Suggestion ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("MyAlias", "my_alias")]
    [InlineData("HTTPSStatus", "https_status")]
    [InlineData("order total", "order_total")]
    [InlineData("123foo", "_123foo")]
    public void Normalize_ConvertsAsExpected(string input, string expected) =>
        Assert.Equal(expected, _sut.Normalize(input));

    [Fact]
    public void Normalize_IsIdempotent()
    {
        string once = _sut.Normalize("MyAlias");
        string twice = _sut.Normalize(once);
        Assert.Equal(once, twice);
    }
}

