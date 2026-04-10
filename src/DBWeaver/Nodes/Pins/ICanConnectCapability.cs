namespace DBWeaver.Nodes.Pins;

public interface ICanConnectCapability
{
    PinConnectionDecision CanConnect(
        PinModel self,
        PinModel other,
        PinConnectionContext context);
}
