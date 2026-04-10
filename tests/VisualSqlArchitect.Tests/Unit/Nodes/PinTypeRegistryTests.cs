using DBWeaver.Nodes;
using DBWeaver.Nodes.PinTypes;
using Xunit;

namespace DBWeaver.Tests.Unit.Nodes;

public class PinTypeRegistryTests
{
    [Theory]
    [InlineData(PinDataType.Text)]
    [InlineData(PinDataType.Integer)]
    [InlineData(PinDataType.Decimal)]
    [InlineData(PinDataType.Number)]
    [InlineData(PinDataType.Boolean)]
    [InlineData(PinDataType.DateTime)]
    [InlineData(PinDataType.Json)]
    [InlineData(PinDataType.ColumnRef)]
    [InlineData(PinDataType.ColumnSet)]
    [InlineData(PinDataType.RowSet)]
    [InlineData(PinDataType.TableDef)]
    [InlineData(PinDataType.ViewDef)]
    [InlineData(PinDataType.ColumnDef)]
    [InlineData(PinDataType.Constraint)]
    [InlineData(PinDataType.IndexDef)]
    [InlineData(PinDataType.TypeDef)]
    [InlineData(PinDataType.SequenceDef)]
    [InlineData(PinDataType.AlterOp)]
    [InlineData(PinDataType.ReportQuery)]
    [InlineData(PinDataType.Expression)]
    public void EnumToTypeToEnum_RoundTrip_PreservesValue(PinDataType type)
    {
        IPinDataType mapped = PinTypeRegistry.GetType(type);

        PinDataType result = PinTypeRegistry.GetEnum(mapped);

        Assert.Equal(type, result);
    }

    [Fact]
    public void CanReceiveFrom_RejectsCrossFamilyBetweenQueryAndDdl()
    {
        IPinDataType ddlTarget = PinTypeRegistry.GetType(PinDataType.TableDef);
        IPinDataType querySource = PinTypeRegistry.GetType(PinDataType.ColumnRef);

        bool accepted = ddlTarget.CanReceiveFrom(querySource);

        Assert.False(accepted);
    }

    [Fact]
    public void CanReceiveFrom_AcceptsWithinSameFamily()
    {
        IPinDataType queryTarget = PinTypeRegistry.GetType(PinDataType.Expression);
        IPinDataType querySource = PinTypeRegistry.GetType(PinDataType.ColumnRef);

        bool accepted = queryTarget.CanReceiveFrom(querySource);

        Assert.True(accepted);
    }
}
