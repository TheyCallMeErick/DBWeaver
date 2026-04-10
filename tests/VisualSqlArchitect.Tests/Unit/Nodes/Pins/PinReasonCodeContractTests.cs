using DBWeaver.Nodes.Pins;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class PinReasonCodeContractTests
{
    [Fact]
    public void ReasonCodes_AreUniqueAndStable()
    {
        var values = Enum.GetValues<PinConnectionReasonCode>();
        var numericValues = values.Select(v => (int)v).ToArray();

        Assert.Equal(numericValues.Length, numericValues.Distinct().Count());
        Assert.Contains(PinConnectionReasonCode.SelfConnectionForbidden, values);
        Assert.Contains(PinConnectionReasonCode.DomainFamilyMismatch, values);
        Assert.Contains(PinConnectionReasonCode.WildcardProjectionOnly, values);
    }
}
