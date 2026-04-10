using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.SidebarLeft;

public class SchemaViewModelTypeMappingTests
{
    [Theory]
    [InlineData("int", PinDataType.Integer)]
    [InlineData("bigint", PinDataType.Integer)]
    [InlineData("decimal", PinDataType.Decimal)]
    [InlineData("float", PinDataType.Decimal)]
    [InlineData("varchar", PinDataType.Text)]
    [InlineData("boolean", PinDataType.Boolean)]
    [InlineData("timestamp", PinDataType.DateTime)]
    [InlineData("jsonb", PinDataType.Json)]
    [InlineData("unknown_type", PinDataType.Expression)]
    public void MapSqlTypeToPinDataType_MapsExpectedType(string rawType, PinDataType expected)
    {
        PinDataType mapped = SchemaViewModel.MapSqlTypeToPinDataType(rawType);

        Assert.Equal(expected, mapped);
    }
}
