using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class PinViewModelRowSetTooltipTests
{
    [Fact]
    public void TooltipTypeDetails_RowSet_IncludesSchemaPreviewFromOwnerColumns()
    {
        var def = new NodeDefinition(
            NodeType.Subquery,
            NodeCategory.DataSource,
            "Synthetic RowSet",
            "test",
            [
                new PinDescriptor("result", PinDirection.Output, PinDataType.RowSet, IsRequired: false),
                new PinDescriptor(
                    "id",
                    PinDirection.Output,
                    PinDataType.ColumnRef,
                    IsRequired: false,
                    ColumnRefMeta: new ColumnRefMeta("id", "s", PinDataType.Integer, false)
                ),
                new PinDescriptor(
                    "name",
                    PinDirection.Output,
                    PinDataType.ColumnRef,
                    IsRequired: false,
                    ColumnRefMeta: new ColumnRefMeta("name", "s", PinDataType.Text, true)
                )
            ],
            []
        );

        var node = new NodeViewModel(def, new Point(0, 0));
        PinViewModel rowsetPin = node.OutputPins.First(p => p.Name == "result");

        string tooltip = rowsetPin.TooltipTypeDetails;

        Assert.Contains("RowSet[2]", tooltip, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id:INT", tooltip, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name:TXT", tooltip, StringComparison.OrdinalIgnoreCase);
    }
}


