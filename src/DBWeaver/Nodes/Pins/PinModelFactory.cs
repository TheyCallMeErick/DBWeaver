namespace DBWeaver.Nodes.Pins;

public static class PinModelFactory
{
    private static readonly ICanConnectCapability CanConnectCapability = new DefaultCanConnectCapability();

    public static PinModel Create(
        PinId pinId,
        PinDescriptor descriptor,
        PinModelOwner owner,
        PinDataType effectiveDataType,
        PinDataType? expectedColumnScalarType)
    {
        if (descriptor.DataType == PinDataType.ColumnRef)
            return new ColumnRefPinModel(pinId, descriptor, owner, effectiveDataType, expectedColumnScalarType, CanConnectCapability);

        if (descriptor.DataType == PinDataType.ColumnSet)
            return new ColumnSetPinModel(pinId, descriptor, owner, effectiveDataType, expectedColumnScalarType, CanConnectCapability);

        if (descriptor.DataType == PinDataType.RowSet)
            return new RowSetPinModel(pinId, descriptor, owner, effectiveDataType, expectedColumnScalarType, CanConnectCapability);

        if (IsDefinitionType(descriptor.DataType))
            return new DefinitionPinModel(pinId, descriptor, owner, effectiveDataType, expectedColumnScalarType, CanConnectCapability);

        return new ScalarPinModel(pinId, descriptor, owner, effectiveDataType, expectedColumnScalarType, CanConnectCapability);
    }

    private static bool IsDefinitionType(PinDataType type) =>
        type
            is PinDataType.TableDef
                or PinDataType.ViewDef
                or PinDataType.ColumnDef
                or PinDataType.Constraint
                or PinDataType.IndexDef
                or PinDataType.TypeDef
                or PinDataType.SequenceDef
                or PinDataType.AlterOp;
}
