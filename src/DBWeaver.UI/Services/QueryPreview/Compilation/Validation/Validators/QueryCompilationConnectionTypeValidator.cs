
namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationConnectionTypeValidator(CanvasViewModel canvas)
{
    private readonly CanvasViewModel _canvas = canvas;

    public void Validate(List<string> errors)
    {
        foreach (ConnectionViewModel connection in _canvas.Connections)
        {
            if (connection.ToPin is null)
                continue;

            PinDataType fromType = connection.FromPin.EffectiveDataType;
            PinDataType toType = connection.ToPin.EffectiveDataType;

            if (IsProjectionSourceConnection(connection, fromType, toType))
                continue;

            if (ArePinsCompatible(fromType, toType))
                continue;

            string fromRef = $"{connection.FromPin.Owner.Title}.{connection.FromPin.Name}";
            string toRef = $"{connection.ToPin.Owner.Title}.{connection.ToPin.Name}";
            errors.Add($"Incompatible connection: {fromRef} ({fromType}) -> {toRef} ({toType}).");
        }
    }

    private static bool IsProjectionSourceConnection(
        ConnectionViewModel connection,
        PinDataType source,
        PinDataType target)
    {
        if (source == PinDataType.ColumnSet
            && target == PinDataType.ColumnRef
            && connection.ToPin is not null
            && connection.ToPin.Owner.Type is NodeType.ColumnList or NodeType.ColumnSetBuilder
            && IsProjectionInputPinName(connection.ToPin.Name)
            && IsWildcardProjectionPin(connection.FromPin))
        {
            return true;
        }

        return source == PinDataType.RowSet
            && target == PinDataType.ColumnRef
            && connection.ToPin is not null
            && connection.ToPin.Owner.Type is NodeType.ColumnList or NodeType.ColumnSetBuilder
            && IsProjectionInputPinName(connection.ToPin.Name);
    }

    private static bool IsWildcardProjectionPin(PinViewModel pin)
    {
        return pin.Owner.Type == NodeType.TableSource
            && pin.EffectiveDataType == PinDataType.ColumnSet
            && pin.Name.Equals("*", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProjectionInputPinName(string? pinName)
    {
        if (string.IsNullOrWhiteSpace(pinName))
            return false;

        return pinName.Equals("columns", StringComparison.OrdinalIgnoreCase)
            || pinName.Equals("metadata", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ArePinsCompatible(PinDataType source, PinDataType target)
    {
        if (IsStructuralMismatch(source, target))
            return false;

        if (source.IsNumericScalar() && target.IsNumericScalar())
            return true;

        if (target == PinDataType.Boolean && source == PinDataType.ColumnRef)
            return false;

        if (source == PinDataType.ColumnRef && (target.IsScalar() || target == PinDataType.Expression))
            return true;

        if (target == PinDataType.ColumnRef && (source.IsScalar() || source == PinDataType.Expression))
            return true;

        if (source == PinDataType.Expression && target.IsScalar())
            return true;

        if (target == PinDataType.Expression && source.IsScalar())
            return true;

        return source == target;
    }

    private static bool IsStructuralMismatch(PinDataType from, PinDataType to)
    {
        bool fromRowSet = from == PinDataType.RowSet;
        bool toRowSet = to == PinDataType.RowSet;
        return fromRowSet != toRowSet;
    }
}


