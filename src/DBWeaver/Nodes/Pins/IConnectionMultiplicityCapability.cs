namespace DBWeaver.Nodes.Pins;

public interface IConnectionMultiplicityCapability
{
    bool AllowsAdditionalConnection(PinModel self, PinConnectionContext context);

    IReadOnlyList<IPinMutation> ResolveConflicts(
        PinModel self,
        PinModel other,
        PinConnectionContext context);
}
