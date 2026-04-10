namespace DBWeaver.Nodes.Pins;

public interface IConnectionLifecycleCapability
{
    IReadOnlyList<IPinMutation> OnConnected(
        PinModel self,
        PinModel other,
        PinConnectionContext context);

    IReadOnlyList<IPinMutation> OnDisconnected(
        PinModel self,
        PinModel other,
        PinConnectionContext context);
}
