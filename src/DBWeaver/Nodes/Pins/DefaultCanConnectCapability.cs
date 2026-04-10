namespace DBWeaver.Nodes.Pins;

public sealed class DefaultCanConnectCapability : ICanConnectCapability
{
    public PinConnectionDecision CanConnect(
        PinModel self,
        PinModel other,
        PinConnectionContext context) =>
        PinCompatibilityEvaluator.Evaluate(self, other, context);
}
