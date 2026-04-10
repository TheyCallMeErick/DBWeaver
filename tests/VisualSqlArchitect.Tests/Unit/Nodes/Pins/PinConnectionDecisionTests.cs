using DBWeaver.Nodes.Pins;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class PinConnectionDecisionTests
{
    [Fact]
    public void Allowed_BuildsSuccessfulDecisionWithDefaults()
    {
        PinConnectionDecision decision = PinConnectionDecision.Allowed();

        Assert.True(decision.IsAllowed);
        Assert.Equal(PinConnectionReasonCode.None, decision.ReasonCode);
        Assert.Empty(decision.Mutations);
        Assert.Empty(decision.Events);
    }

    [Fact]
    public void Rejected_BuildsRejectedDecisionWithExpectedReasonCode()
    {
        PinConnectionDecision decision = PinConnectionDecision.Rejected(PinConnectionReasonCode.SameDirectionForbidden);

        Assert.False(decision.IsAllowed);
        Assert.Equal(PinConnectionReasonCode.SameDirectionForbidden, decision.ReasonCode);
    }
}
