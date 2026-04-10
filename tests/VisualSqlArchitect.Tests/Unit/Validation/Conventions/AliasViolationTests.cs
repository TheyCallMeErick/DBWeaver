using DBWeaver.UI.ViewModels.Validation.Conventions;

namespace DBWeaver.Tests.Unit.Validation.Conventions;

public class AliasViolationTests
{
    [Fact]
    public void AliasViolation_StoresValues()
    {
        var violation = new AliasViolation("CODE", "Message", "Suggestion");
        Assert.Equal("CODE", violation.Code);
        Assert.Equal("Message", violation.Message);
        Assert.Equal("Suggestion", violation.Suggestion);
    }

    [Fact]
    public void AliasViolation_EqualityByValue()
    {
        var a = new AliasViolation("CODE", "Message", "Suggestion");
        var b = new AliasViolation("CODE", "Message", "Suggestion");
        Assert.Equal(a, b);
    }
}

