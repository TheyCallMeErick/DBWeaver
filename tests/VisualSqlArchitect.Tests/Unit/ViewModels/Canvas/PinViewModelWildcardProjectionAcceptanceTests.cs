using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class PinViewModelWildcardProjectionAcceptanceTests
{
    [Fact]
    public void CanAccept_AllowsTableWildcardIntoColumnListColumns()
    {
        NodeViewModel table = new("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        NodeViewModel columnList = new(NodeDefinitionRegistry.Get(NodeType.ColumnList), new Point(120, 0));

        PinViewModel star = table.OutputPins.First(p => p.Name == "*");
        PinViewModel columns = columnList.InputPins.First(p => p.Name == "columns");

        Assert.True(columns.CanAccept(star));
    }

    [Fact]
    public void CanAccept_RejectsTableWildcardIntoNonProjectionColumnRefPin()
    {
        NodeViewModel table = new("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        NodeViewModel equals = new(NodeDefinitionRegistry.Get(NodeType.Equals), new Point(180, 0));

        PinViewModel star = table.OutputPins.First(p => p.Name == "*");
        PinViewModel left = equals.InputPins.First(p => p.Name == "left");

        Assert.False(left.CanAccept(star));
    }

    [Fact]
    public void CanAccept_RejectsNonTableColumnSetIntoColumnListColumns()
    {
        NodeViewModel columnSetBuilder = new(NodeDefinitionRegistry.Get(NodeType.ColumnSetBuilder), new Point(0, 0));
        NodeViewModel columnList = new(NodeDefinitionRegistry.Get(NodeType.ColumnList), new Point(160, 0));

        PinViewModel resultSet = columnSetBuilder.OutputPins.First(p => p.Name == "result");
        PinViewModel columns = columnList.InputPins.First(p => p.Name == "columns");

        Assert.False(columns.CanAccept(resultSet));
    }
}


