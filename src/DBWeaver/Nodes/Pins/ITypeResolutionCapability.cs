namespace DBWeaver.Nodes.Pins;

public interface ITypeResolutionCapability
{
    PinDataType ResolveEffectiveType(PinModel self, PinConnectionContext context);
}
