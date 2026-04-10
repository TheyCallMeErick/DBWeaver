namespace DBWeaver.Nodes.Pins;

public sealed record PruneConnectionsMutation(
    IReadOnlyList<string> PrunedConnectionIds,
    string PruneReason) : IPinMutation
{
    public PinConnectionReasonCode ReasonCode => PinConnectionReasonCode.WildcardProjectionOnly;
}
