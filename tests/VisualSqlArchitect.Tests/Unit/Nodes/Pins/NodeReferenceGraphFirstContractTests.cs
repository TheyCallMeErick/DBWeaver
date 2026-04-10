using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class NodeReferenceGraphFirstContractTests
{
    [Fact]
    public void TableReference_Definition_ExposesStructuralReferencePins()
    {
        NodeDefinition definition = NodeDefinitionRegistry.Get(NodeType.TableReference);

        Assert.Equal(NodeCategory.Ddl, definition.Category);
        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Output && p.Name == "table_ref" && p.DataType == PinDataType.TableDef);
        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Output && p.Name == "*" && p.DataType == PinDataType.ColumnSet);
    }

    [Fact]
    public void ViewReference_Definition_ExposesStructuralReferencePins()
    {
        NodeDefinition definition = NodeDefinitionRegistry.Get(NodeType.ViewReference);

        Assert.Equal(NodeCategory.Ddl, definition.Category);
        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Output && p.Name == "view_ref" && p.DataType == PinDataType.ViewDef);
        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Output && p.Name == "*" && p.DataType == PinDataType.ColumnSet);
    }

    [Fact]
    public void AlterOperations_AcceptTableReferenceAsTargetInput()
    {
        var tableReference = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableReference), new Point(0, 0));
        var renameTableOp = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.RenameTableOp), new Point(200, 0));

        PinViewModel source = tableReference.OutputPins.Single(p => p.Name == "table_ref");
        PinViewModel destination = renameTableOp.InputPins.Single(p => p.Name == "target_table");

        Assert.True(destination.EvaluateConnection(source).IsAllowed);
    }
}
