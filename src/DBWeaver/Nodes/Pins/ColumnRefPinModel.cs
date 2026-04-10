namespace DBWeaver.Nodes.Pins;

public sealed class ColumnRefPinModel : PinModel, ISchemaCapability
{
    public ColumnRefPinModel(
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

    public ColumnRefMeta? ColumnRef => ColumnRefMeta;
    public ColumnSetMeta? ColumnSet => null;
}
