namespace DBWeaver.Nodes.Pins;

public sealed record ReplaceExistingConnectionMutation(
    PinId TargetPinId,
    IReadOnlyList<string> ReplacedConnectionIds) : IPinMutation
{
    public PinConnectionReasonCode ReasonCode => PinConnectionReasonCode.MultiplicityExceeded;
}
