using DBWeaver.Nodes;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class SetOperationGraphFirstContractTests
{
    [Fact]
    public void SetOperation_Definition_ExposesStructuredInputsForGraphFirstComposition()
    {
        NodeDefinition definition = NodeDefinitionRegistry.Get(NodeType.SetOperation);

        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Input && p.Name == "left" && p.DataType == PinDataType.RowSet);
        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Input && p.Name == "right" && p.DataType == PinDataType.RowSet);
        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Input && p.Name == "operator_text" && p.DataType == PinDataType.Text);
        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Input && p.Name == "query_text" && p.DataType == PinDataType.Text);
    }
}
