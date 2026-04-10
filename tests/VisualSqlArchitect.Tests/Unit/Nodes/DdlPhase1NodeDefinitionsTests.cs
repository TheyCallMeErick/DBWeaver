using DBWeaver.Nodes;
using DBWeaver.Nodes.Definitions;
using Xunit;

namespace DBWeaver.Tests.Unit.Nodes;

public class DdlPhase1NodeDefinitionsTests
{
    [Fact]
    public void DdlDefinitions_ExposeAtLeastPhase1Set()
    {
        // Keep this test resilient as DDL nodes expand across roadmap phases.
        Assert.True(DdlDefinitions.AllTypes.Count >= 15);
        Assert.Equal(DdlDefinitions.AllTypes.Count, DdlDefinitions.All.Count);
    }

    [Theory]
    [InlineData(NodeType.TableDefinition)]
    [InlineData(NodeType.ColumnDefinition)]
    [InlineData(NodeType.PrimaryKeyConstraint)]
    [InlineData(NodeType.ForeignKeyConstraint)]
    [InlineData(NodeType.UniqueConstraint)]
    [InlineData(NodeType.CheckConstraint)]
    [InlineData(NodeType.DefaultConstraint)]
    [InlineData(NodeType.IndexDefinition)]
    [InlineData(NodeType.CreateTableOutput)]
    [InlineData(NodeType.AlterTableOutput)]
    [InlineData(NodeType.CreateIndexOutput)]
    [InlineData(NodeType.AddColumnOp)]
    [InlineData(NodeType.DropColumnOp)]
    [InlineData(NodeType.RenameColumnOp)]
    [InlineData(NodeType.AlterColumnTypeOp)]
    public void DdlNode_DefinitionExists_AndUsesDdlCategory(NodeType type)
    {
        NodeDefinition definition = NodeDefinitionRegistry.Get(type);

        Assert.Equal(NodeCategory.Ddl, definition.Category);
    }

    [Fact]
    public void DdlNodePins_KeepExpectedPinFamilies()
    {
        NodeDefinition table = NodeDefinitionRegistry.Get(NodeType.TableDefinition);
        Assert.Contains(table.InputPins, p => p.Name == "column" && p.DataType == PinDataType.ColumnDef && p.AllowMultiple);
        Assert.Contains(table.OutputPins, p => p.Name == "table" && p.DataType == PinDataType.TableDef);

        NodeDefinition index = NodeDefinitionRegistry.Get(NodeType.IndexDefinition);
        Assert.Contains(index.InputPins, p => p.Name == "table" && p.DataType == PinDataType.TableDef);
        Assert.Contains(index.InputPins, p => p.Name == "column" && p.DataType == PinDataType.ColumnDef && p.AllowMultiple);
        Assert.Contains(index.OutputPins, p => p.Name == "idx" && p.DataType == PinDataType.IndexDef);

        NodeDefinition alterOutput = NodeDefinitionRegistry.Get(NodeType.AlterTableOutput);
        Assert.Contains(alterOutput.InputPins, p => p.Name == "operation" && p.DataType == PinDataType.AlterOp && p.AllowMultiple);
    }

    [Fact]
    public void Get_UnknownNodeType_ThrowsExpectedError()
    {
        Assert.Throws<KeyNotFoundException>(() => NodeDefinitionRegistry.Get((NodeType)(-9999)));
    }
}
