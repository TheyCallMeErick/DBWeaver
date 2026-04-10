namespace DBWeaver.Nodes.Pins;

public abstract class PinModel
{
    private readonly ICanConnectCapability _canConnectCapability;

    protected PinModel(
        PinId pinId,
        PinDescriptor descriptor,
        PinModelOwner owner,
        PinDataType effectiveDataType,
        PinDataType? expectedColumnScalarType,
        ICanConnectCapability canConnectCapability)
    {
        PinId = pinId;
        Descriptor = descriptor;
        Owner = owner;
        EffectiveDataType = effectiveDataType;
        ExpectedColumnScalarType = expectedColumnScalarType;
        _canConnectCapability = canConnectCapability;
    }

    public PinId PinId { get; }
    public PinDescriptor Descriptor { get; }
    public string Name => Descriptor.Name;
    public PinDirection Direction => Descriptor.Direction;
    public PinDataType DeclaredDataType => Descriptor.DataType;
    public PinDataType EffectiveDataType { get; }
    public bool IsRequired => Descriptor.IsRequired;
    public bool AllowMultiple => Descriptor.AllowMultiple;
    public PinModelOwner Owner { get; }
    public PinDataType? ExpectedColumnScalarType { get; }

    public virtual ColumnRefMeta? ColumnRefMeta => Descriptor.ColumnRefMeta;
    public virtual ColumnSetMeta? ColumnSetMeta => Descriptor.ColumnSetMeta;

    public virtual PinConnectionDecision CanConnect(
        PinModel other,
        PinConnectionContext context) =>
        _canConnectCapability.CanConnect(this, other, context);
}
