using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using Avalonia.Media;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class DdlPhase2PinViewModelCompatibilityTests
{
    [Fact]
    public void CanAccept_RejectsCrossFamilyConnection_DdlToQuery()
    {
        var ddlNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(0, 0));
        var queryNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Sum), new Point(100, 0));

        PinViewModel ddlOutput = ddlNode.OutputPins.First(p => p.DataType == PinDataType.TableDef);
        PinViewModel queryInput = queryNode.InputPins.First(p => p.Name == "value");

        Assert.False(queryInput.CanAccept(ddlOutput));
    }

    [Fact]
    public void DataTypeLabel_UsesExpectedDdlAbbreviations()
    {
        var tableDefinition = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(0, 0));
        var alterTableOutput = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.AlterTableOutput), new Point(0, 0));

        Assert.Equal("TBL", tableDefinition.OutputPins.First(p => p.DataType == PinDataType.TableDef).DataTypeLabel);
        Assert.Equal("COL", tableDefinition.InputPins.First(p => p.DataType == PinDataType.ColumnDef).DataTypeLabel);
        Assert.Equal("CON", tableDefinition.InputPins.First(p => p.DataType == PinDataType.Constraint).DataTypeLabel);
        Assert.Equal("ALT", alterTableOutput.InputPins.First(p => p.DataType == PinDataType.AlterOp).DataTypeLabel);

        var indexDefinition = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.IndexDefinition), new Point(0, 0));
        Assert.Equal("IDX", indexDefinition.OutputPins.First(p => p.DataType == PinDataType.IndexDef).DataTypeLabel);
    }

    [Fact]
    public void PinColor_MapsDdlTypesToExpectedPalette()
    {
        var tableDefinition = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(0, 0));
        var alterTableOutput = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.AlterTableOutput), new Point(0, 0));

        Assert.Equal(Color.Parse("#2563EB"), tableDefinition.OutputPins.First(p => p.DataType == PinDataType.TableDef).PinColor);
        Assert.Equal(Color.Parse("#16A34A"), tableDefinition.InputPins.First(p => p.DataType == PinDataType.ColumnDef).PinColor);
        Assert.Equal(Color.Parse("#A78BFA"), tableDefinition.InputPins.First(p => p.DataType == PinDataType.Constraint).PinColor);
        Assert.Equal(Color.Parse("#F59E0B"), alterTableOutput.InputPins.First(p => p.DataType == PinDataType.AlterOp).PinColor);

        var indexDefinition = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.IndexDefinition), new Point(0, 0));
        Assert.Equal(Color.Parse("#93C5FD"), indexDefinition.OutputPins.First(p => p.DataType == PinDataType.IndexDef).PinColor);
    }
}


