namespace DBWeaver.Nodes.Pins;

public sealed class OutputPinModel : PinModel
{
    public OutputPinModel(
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
        if (descriptor.Direction != PinDirection.Output)
            throw new ArgumentException("OutputPinModel requires an output descriptor.", nameof(descriptor));
    }
}
