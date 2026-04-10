namespace DBWeaver.Nodes.Pins;

public sealed class ColumnSetPinModel : PinModel, ISchemaCapability
{
    public ColumnSetPinModel(
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

    public ColumnRefMeta? ColumnRef => null;
    public ColumnSetMeta? ColumnSet => ColumnSetMeta;
}
