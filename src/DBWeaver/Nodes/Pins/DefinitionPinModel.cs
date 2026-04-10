namespace DBWeaver.Nodes.Pins;

public sealed class DefinitionPinModel : PinModel
{
    public DefinitionPinModel(
        PinId pinId,
        PinDescriptor descriptor,
        PinModelOwner owner,
        PinDataType effectiveDataType,
        PinDataType? expectedColumnScalarType,
        ICanConnectCapability canConnectCapability)
        : base(
            pinId,
            descriptor,
            owner,
            effectiveDataType,
            expectedColumnScalarType,
            canConnectCapability)
    {
    }
}
