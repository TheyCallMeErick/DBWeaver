namespace VisualSqlArchitect.Nodes;

public static class PinDataTypeExtensions
{
    public static bool IsScalar(this PinDataType type) =>
        type
            is PinDataType.Text
                or PinDataType.Integer
                or PinDataType.Decimal
                or PinDataType.Number
                or PinDataType.Boolean
                or PinDataType.DateTime
                or PinDataType.Json;

    public static bool IsNumericScalar(this PinDataType type) =>
        type is PinDataType.Integer or PinDataType.Decimal or PinDataType.Number;

    public static bool IsStructural(this PinDataType type) =>
        type is PinDataType.ColumnRef or PinDataType.ColumnSet or PinDataType.RowSet;
}
