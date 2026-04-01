using VisualSqlArchitect.Nodes;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Nodes;

public class Phase5AdvancedStructuralNodeTests
{
    [Fact]
    public void Registry_Contains_New_Phase5_NodeDefinitions()
    {
        NodeDefinition rowSetJoin = NodeDefinitionRegistry.Get(NodeType.RowSetJoin);
        NodeDefinition rowSetFilter = NodeDefinitionRegistry.Get(NodeType.RowSetFilter);
        NodeDefinition rowSetAggregate = NodeDefinitionRegistry.Get(NodeType.RowSetAggregate);
        NodeDefinition columnRefCast = NodeDefinitionRegistry.Get(NodeType.ColumnRefCast);
        NodeDefinition scalarFromColumn = NodeDefinitionRegistry.Get(NodeType.ScalarFromColumn);

        Assert.Equal(NodeCategory.DataSource, rowSetJoin.Category);
        Assert.Contains(rowSetJoin.Pins, p => p.Name == "left" && p.DataType == PinDataType.RowSet);
        Assert.Contains(rowSetJoin.Pins, p => p.Name == "right" && p.DataType == PinDataType.RowSet);
        Assert.Contains(rowSetJoin.Pins, p => p.Name == "result" && p.DataType == PinDataType.RowSet);

        Assert.Equal(NodeCategory.ResultModifier, rowSetFilter.Category);
        Assert.Contains(rowSetFilter.Pins, p => p.Name == "source" && p.DataType == PinDataType.RowSet);
        Assert.Contains(rowSetFilter.Pins, p => p.Name == "conditions" && p.DataType == PinDataType.Boolean);

        Assert.Equal(NodeCategory.ResultModifier, rowSetAggregate.Category);
        Assert.Contains(rowSetAggregate.Pins, p => p.Name == "source" && p.DataType == PinDataType.RowSet);
        Assert.Contains(rowSetAggregate.Pins, p => p.Name == "group_by" && p.DataType == PinDataType.ColumnRef);
        Assert.Contains(rowSetAggregate.Pins, p => p.Name == "metrics" && p.DataType == PinDataType.ColumnRef);

        Assert.Equal(NodeCategory.TypeCast, columnRefCast.Category);
        Assert.Contains(columnRefCast.Pins, p => p.Name == "value" && p.DataType == PinDataType.ColumnRef);
        Assert.Contains(columnRefCast.Pins, p => p.Name == "result" && p.DataType == PinDataType.Expression);

        Assert.Equal(NodeCategory.TypeCast, scalarFromColumn.Category);
        Assert.Contains(scalarFromColumn.Pins, p => p.Name == "value" && p.DataType == PinDataType.ColumnRef);
        Assert.Contains(scalarFromColumn.Pins, p => p.Name == "result" && p.DataType == PinDataType.Expression);
    }
}
