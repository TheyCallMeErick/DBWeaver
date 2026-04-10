using DBWeaver.Nodes;
using DBWeaver.Nodes.Pins;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class PinModelFactoryTests
{
    [Theory]
    [InlineData(PinDataType.ColumnRef, typeof(ColumnRefPinModel))]
    [InlineData(PinDataType.ColumnSet, typeof(ColumnSetPinModel))]
    [InlineData(PinDataType.RowSet, typeof(RowSetPinModel))]
    [InlineData(PinDataType.TableDef, typeof(DefinitionPinModel))]
    [InlineData(PinDataType.Number, typeof(ScalarPinModel))]
    public void Create_ReturnsExpectedConcreteModel(PinDataType dataType, Type expectedType)
    {
        string nodeId = $"node-{Guid.NewGuid():N}";
        var descriptor = new PinDescriptor("pin", PinDirection.Input, dataType);
        var owner = new PinModelOwner(nodeId, NodeType.Sum);

        PinModel model = PinModelFactory.Create(
            new PinId($"{nodeId}:pin:Input"),
            descriptor,
            owner,
            dataType,
            expectedColumnScalarType: null);

        Assert.Equal(expectedType, model.GetType());
    }
}
