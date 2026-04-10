using DBWeaver.UI.ViewModels.Validation.Conventions;
using DBWeaver.UI.ViewModels.Validation.Conventions.Implementations;

namespace DBWeaver.Tests.Unit.Validation.Conventions;

public class IAliasConventionDefaultMethodTests
{
    [Fact]
    public void IsCompliant_DefaultMethod_UsesCheckResult()
    {
        IAliasConvention convention = new SnakeCaseConvention();

        Assert.True(convention.IsCompliant("order_total"));
        Assert.False(convention.IsCompliant("OrderTotal"));
    }
}

