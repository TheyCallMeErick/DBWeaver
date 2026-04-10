namespace DBWeaver.Nodes.Pins;

public sealed class InputPinModel : PinModel
{
    public InputPinModel(
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
        if (descriptor.Direction != PinDirection.Input)
            throw new ArgumentException("InputPinModel requires an input descriptor.", nameof(descriptor));
    }
}
