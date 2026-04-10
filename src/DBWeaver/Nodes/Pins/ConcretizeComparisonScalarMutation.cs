namespace DBWeaver.Nodes.Pins;

public sealed record ConcretizeComparisonScalarMutation(
    string NodeId,
    PinDataType ScalarType) : IPinMutation
{
    public PinConnectionReasonCode ReasonCode => PinConnectionReasonCode.None;
}
