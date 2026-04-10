namespace DBWeaver.Nodes.PinTypes;

/// <summary>
/// Registry and adapter for <see cref="IPinDataType"/> implementations.
/// Provides mapping between legacy <see cref="PinDataType"/> enum values and <see cref="IPinDataType"/> instances.
/// Used during serialization round-trips and for backward compatibility.
/// </summary>
public static class PinTypeRegistry
{
    private sealed class StandardPinDataType(
        PinDataType enumType,
        string visualColorHex,
        double wireThickness,
        PinWireDashKind wireDashKind,
        bool isDdlFamily
    ) : IPinDataType
    {
        public PinDataType EnumType { get; } = enumType;
        public string Name => EnumType.ToString();
        public string VisualColorHex { get; } = visualColorHex;
        public double WireThickness { get; } = wireThickness;
        public PinWireDashKind WireDashKind { get; } = wireDashKind;
        public bool IsDdlFamily { get; } = isDdlFamily;

        public bool CanReceiveFrom(IPinDataType source)
        {
            if (source is not StandardPinDataType src)
                return false;

            return IsDdlFamily == src.IsDdlFamily;
        }
    }

    private static readonly IReadOnlyDictionary<PinDataType, StandardPinDataType> _types =
        new Dictionary<PinDataType, StandardPinDataType>
        {
            [PinDataType.Text] = new(PinDataType.Text, NodeVisualColorConstants.PinText, 1.8, PinWireDashKind.Solid, false),
            [PinDataType.Integer] = new(PinDataType.Integer, NodeVisualColorConstants.PinInteger, 1.8, PinWireDashKind.Solid, false),
            [PinDataType.Decimal] = new(PinDataType.Decimal, NodeVisualColorConstants.PinDecimal, 1.8, PinWireDashKind.Solid, false),
            [PinDataType.Number] = new(PinDataType.Number, NodeVisualColorConstants.PinNumber, 1.8, PinWireDashKind.Solid, false),
            [PinDataType.Boolean] = new(PinDataType.Boolean, NodeVisualColorConstants.PinBoolean, 1.8, PinWireDashKind.Solid, false),
            [PinDataType.DateTime] = new(PinDataType.DateTime, NodeVisualColorConstants.PinDateTime, 1.8, PinWireDashKind.Solid, false),
            [PinDataType.Json] = new(PinDataType.Json, NodeVisualColorConstants.PinJson, 1.8, PinWireDashKind.Solid, false),
            [PinDataType.ColumnRef] = new(PinDataType.ColumnRef, NodeVisualColorConstants.PinColumnRef, 2.0, PinWireDashKind.Solid, false),
            [PinDataType.ColumnSet] = new(PinDataType.ColumnSet, NodeVisualColorConstants.PinColumnSet, 2.2, PinWireDashKind.LongDash, false),
            [PinDataType.RowSet] = new(PinDataType.RowSet, NodeVisualColorConstants.PinRowSet, 2.5, PinWireDashKind.WideDash, false),
            [PinDataType.TableDef] = new(PinDataType.TableDef, NodeVisualColorConstants.PinTableDef, 2.5, PinWireDashKind.Solid, true),
            [PinDataType.ViewDef] = new(PinDataType.ViewDef, NodeVisualColorConstants.PinViewDef, 2.4, PinWireDashKind.Solid, true),
            [PinDataType.ColumnDef] = new(PinDataType.ColumnDef, NodeVisualColorConstants.PinColumnDef, 2.0, PinWireDashKind.Solid, true),
            [PinDataType.Constraint] = new(PinDataType.Constraint, NodeVisualColorConstants.PinConstraint, 2.2, PinWireDashKind.MediumDash, true),
            [PinDataType.IndexDef] = new(PinDataType.IndexDef, NodeVisualColorConstants.PinIndexDef, 1.8, PinWireDashKind.ShortDash, true),
            [PinDataType.TypeDef] = new(PinDataType.TypeDef, NodeVisualColorConstants.PinTypeDef, 2.2, PinWireDashKind.MediumDash, true),
            [PinDataType.SequenceDef] = new(PinDataType.SequenceDef, NodeVisualColorConstants.PinSequenceDef, 2.2, PinWireDashKind.Solid, true),
            [PinDataType.AlterOp] = new(PinDataType.AlterOp, NodeVisualColorConstants.PinAlterOp, 2.2, PinWireDashKind.LongDash, true),
            [PinDataType.ReportQuery] = new(PinDataType.ReportQuery, NodeVisualColorConstants.PinReportQuery, 2.3, PinWireDashKind.MediumDash, false),
            [PinDataType.Expression] = new(PinDataType.Expression, NodeVisualColorConstants.PinExpression, 1.5, PinWireDashKind.Dotted, false),
        };

    public static IPinDataType GetType(PinDataType type)
        => _types.TryGetValue(type, out StandardPinDataType? value)
            ? value
            : _types[PinDataType.Expression];

    public static PinDataType GetEnum(IPinDataType type)
    {
        if (type is StandardPinDataType mapped)
            return mapped.EnumType;

        foreach ((PinDataType key, StandardPinDataType value) in _types)
            if (string.Equals(value.Name, type.Name, StringComparison.Ordinal))
                return key;

        return PinDataType.Expression;
    }

    public static bool IsDdlFamily(PinDataType type)
        => GetType(type).CanReceiveFrom(GetType(PinDataType.TableDef));
}
