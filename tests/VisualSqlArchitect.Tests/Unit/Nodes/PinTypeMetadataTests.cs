using VisualSqlArchitect.Nodes;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Nodes;

public class PinTypeMetadataTests
{
    [Fact]
    public void PinDescriptor_CanCarryColumnRefMeta()
    {
        var meta = new ColumnRefMeta("customer_id", "o", PinDataType.Integer, IsNullable: false);
        var descriptor = new PinDescriptor("customer_id", PinDirection.Output, PinDataType.ColumnRef, ColumnRefMeta: meta);

        Assert.NotNull(descriptor.ColumnRefMeta);
        Assert.Equal("customer_id", descriptor.ColumnRefMeta!.ColumnName);
        Assert.Equal(PinDataType.Integer, descriptor.ColumnRefMeta.ScalarType);
    }

    [Fact]
    public void PinDescriptor_CanCarryColumnSetMeta()
    {
        var meta = new ColumnSetMeta([
            new ColumnRefMeta("id", "o", PinDataType.Integer, IsNullable: false),
            new ColumnRefMeta("name", "o", PinDataType.Text, IsNullable: true)
        ]);
        var descriptor = new PinDescriptor("columns", PinDirection.Output, PinDataType.ColumnSet, ColumnSetMeta: meta);

        Assert.NotNull(descriptor.ColumnSetMeta);
        Assert.Equal(2, descriptor.ColumnSetMeta!.Columns.Count);
    }
}
