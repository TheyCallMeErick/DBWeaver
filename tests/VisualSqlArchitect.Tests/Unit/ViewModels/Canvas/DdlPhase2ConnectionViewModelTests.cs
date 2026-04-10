using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class DdlPhase2ConnectionViewModelTests
{
    [Fact]
    public void DashKind_MapsDdlFamiliesToExpectedStyles()
    {
        var tableDefinition = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(0, 0));
        var primaryKey = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.PrimaryKeyConstraint), new Point(0, 0));
        var indexDefinition = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.IndexDefinition), new Point(0, 0));
        var addColumnOp = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.AddColumnOp), new Point(0, 0));

        var tableWire = new ConnectionViewModel(
            tableDefinition.OutputPins.First(p => p.DataType == PinDataType.TableDef),
            new Point(0, 0),
            new Point(10, 10)
        );
        var constraintWire = new ConnectionViewModel(
            primaryKey.OutputPins.First(p => p.DataType == PinDataType.Constraint),
            new Point(0, 0),
            new Point(10, 10)
        );
        var indexWire = new ConnectionViewModel(
            indexDefinition.OutputPins.First(p => p.DataType == PinDataType.IndexDef),
            new Point(0, 0),
            new Point(10, 10)
        );
        var alterWire = new ConnectionViewModel(
            addColumnOp.OutputPins.First(p => p.DataType == PinDataType.AlterOp),
            new Point(0, 0),
            new Point(10, 10)
        );

        Assert.Equal(ConnectionViewModel.EWireDashKind.Solid, tableWire.DashKind);
        Assert.Equal(ConnectionViewModel.EWireDashKind.MediumDash, constraintWire.DashKind);
        Assert.Equal(ConnectionViewModel.EWireDashKind.ShortDash, indexWire.DashKind);
        Assert.Equal(ConnectionViewModel.EWireDashKind.LongDash, alterWire.DashKind);
    }

    [Fact]
    public void WireThickness_MapsDdlFamiliesToExpectedWidths()
    {
        var tableDefinition = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(0, 0));
        var columnDefinition = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(0, 0));
        var primaryKey = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.PrimaryKeyConstraint), new Point(0, 0));
        var indexDefinition = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.IndexDefinition), new Point(0, 0));
        var addColumnOp = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.AddColumnOp), new Point(0, 0));

        var tableWire = new ConnectionViewModel(
            tableDefinition.OutputPins.First(p => p.DataType == PinDataType.TableDef),
            new Point(0, 0),
            new Point(10, 10)
        );
        var columnWire = new ConnectionViewModel(
            columnDefinition.OutputPins.First(p => p.DataType == PinDataType.ColumnDef),
            new Point(0, 0),
            new Point(10, 10)
        );
        var constraintWire = new ConnectionViewModel(
            primaryKey.OutputPins.First(p => p.DataType == PinDataType.Constraint),
            new Point(0, 0),
            new Point(10, 10)
        );
        var indexWire = new ConnectionViewModel(
            indexDefinition.OutputPins.First(p => p.DataType == PinDataType.IndexDef),
            new Point(0, 0),
            new Point(10, 10)
        );
        var alterWire = new ConnectionViewModel(
            addColumnOp.OutputPins.First(p => p.DataType == PinDataType.AlterOp),
            new Point(0, 0),
            new Point(10, 10)
        );

        Assert.Equal(2.5, tableWire.WireThickness, 3);
        Assert.Equal(2.0, columnWire.WireThickness, 3);
        Assert.Equal(2.2, constraintWire.WireThickness, 3);
        Assert.Equal(1.8, indexWire.WireThickness, 3);
        Assert.Equal(2.2, alterWire.WireThickness, 3);
    }
}


