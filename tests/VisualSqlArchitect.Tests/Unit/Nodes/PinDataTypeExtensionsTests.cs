using Xunit;

namespace DBWeaver.Tests.Unit.Nodes;

public class PinDataTypeExtensionsTests
{
    [Theory]
    [InlineData(PinDataType.Text)]
    [InlineData(PinDataType.Integer)]
    [InlineData(PinDataType.Decimal)]
    [InlineData(PinDataType.Number)]
    [InlineData(PinDataType.Boolean)]
    [InlineData(PinDataType.DateTime)]
    [InlineData(PinDataType.Json)]
    public void IsScalar_ReturnsTrue_ForScalarTypes(PinDataType type)
    {
        Assert.True(type.IsScalar());
    }

    [Theory]
    [InlineData(PinDataType.ColumnRef)]
    [InlineData(PinDataType.ColumnSet)]
    [InlineData(PinDataType.RowSet)]
    [InlineData(PinDataType.TableDef)]
    [InlineData(PinDataType.ColumnDef)]
    [InlineData(PinDataType.Constraint)]
    [InlineData(PinDataType.IndexDef)]
    [InlineData(PinDataType.AlterOp)]
    public void IsStructural_ReturnsTrue_ForStructuralTypes(PinDataType type)
    {
        Assert.True(type.IsStructural());
    }

    [Theory]
    [InlineData(PinDataType.Integer)]
    [InlineData(PinDataType.Decimal)]
    [InlineData(PinDataType.Number)]
    public void IsNumericScalar_ReturnsTrue_ForNumericScalarTypes(PinDataType type)
    {
        Assert.True(type.IsNumericScalar());
    }
}
