using DBWeaver.Nodes;
using DBWeaver.Nodes.Pins;

namespace DBWeaver.UI.ViewModels;

public static class PinDomainAdapter
{
    public static PinModel ToPinModel(PinViewModel pin)
    {
        ArgumentNullException.ThrowIfNull(pin);

        var descriptor = new PinDescriptor(
            pin.Name,
            pin.Direction,
            pin.DataType,
            pin.IsRequired,
            Description: null,
            pin.AllowMultiple,
            pin.ColumnRefMeta,
            pin.ColumnSetMeta);

        var owner = new PinModelOwner(pin.Owner.Id, pin.Owner.Type);
        PinId pinId = ToPinId(pin);

        return PinModelFactory.Create(
            pinId,
            descriptor,
            owner,
            pin.DataType,
            pin.ExpectedColumnScalarType);
    }

    public static PinConnectionDecision CanConnect(PinViewModel target, PinViewModel other)
        => CanConnect(target, other, [], allowImplicitReplacement: false);

    public static PinConnectionDecision CanConnect(
        PinViewModel target,
        PinViewModel other,
        IReadOnlyCollection<ConnectionViewModel> existingConnections,
        bool allowImplicitReplacement)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(other);
        ArgumentNullException.ThrowIfNull(existingConnections);

        PinModel targetModel = ToPinModel(target);
        PinModel otherModel = ToPinModel(other);

        PinConnectionContext context = BuildContextFromConnections(existingConnections, allowImplicitReplacement);
        return targetModel.CanConnect(otherModel, context);
    }

    public static PinConnectionContext BuildContextFromConnections(
        IReadOnlyCollection<ConnectionViewModel> existingConnections,
        bool allowImplicitReplacement)
    {
        List<PinConnectionSnapshot> snapshots = existingConnections
            .Where(c => c.ToPin is not null)
            .Select(c =>
            {
                PinViewModel destinationPin = c.ToPin!;
                return new PinConnectionSnapshot(
                    c.Id,
                    ToPinId(c.FromPin),
                    ToPinId(destinationPin),
                    c.FromPin.Owner.Id,
                    destinationPin.Owner.Id,
                    c.FromPin.Name,
                    destinationPin.Name,
                    c.FromPin.DataType,
                    destinationPin.DataType,
                    ResolveSourceScalarType(c.FromPin));
            })
            .ToList();

        Dictionary<PinId, PinConnectionSnapshot[]> byPin = snapshots
            .SelectMany(snapshot => new[]
            {
                new KeyValuePair<PinId, PinConnectionSnapshot>(snapshot.SourcePinId, snapshot),
                new KeyValuePair<PinId, PinConnectionSnapshot>(snapshot.DestinationPinId, snapshot),
            })
            .GroupBy(x => x.Key)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.Value).ToArray());

        var data = new PinConnectionContextData(
            snapshots,
            byPin,
            IsValidationOnly: true,
            AllowImplicitReplacement: allowImplicitReplacement,
            ComparisonState: null,
            WildcardContext: null);

        return new PinConnectionContext(data, new Dictionary<string, object?>());
    }

    private static PinDataType? ResolveSourceScalarType(PinViewModel source)
    {
        if (source.DataType == PinDataType.ColumnRef)
            return source.ColumnRefMeta?.ScalarType ?? source.ExpectedColumnScalarType;

        if (source.DataType.IsScalar())
            return source.DataType;

        return null;
    }

    private static PinId ToPinId(PinViewModel pin) =>
        new($"{pin.Owner.Id}:{pin.Name}:{pin.Direction}");
}
