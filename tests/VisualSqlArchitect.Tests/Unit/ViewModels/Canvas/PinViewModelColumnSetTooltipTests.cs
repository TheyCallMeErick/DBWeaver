using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class PinViewModelColumnSetTooltipTests
{
    [Fact]
    public void ColumnSetTooltipPreview_ExposesStructuredItems()
    {
        var meta = new ColumnSetMeta(
        [
            new ColumnRefMeta("id", "u", PinDataType.Integer, false),
            new ColumnRefMeta("name", "u", PinDataType.Text, true),
            new ColumnRefMeta("created_at", "u", PinDataType.DateTime, false),
        ]);

        var def = new NodeDefinition(
            NodeType.ColumnSetBuilder,
            NodeCategory.DataSource,
            "Column Set Builder",
            "test",
            [new PinDescriptor("result", PinDirection.Output, PinDataType.ColumnSet, IsRequired: false, ColumnSetMeta: meta)],
            []);

        var node = new NodeViewModel(def, new Point(0, 0));
        PinViewModel pin = node.OutputPins.First(p => p.Name == "result");

        Assert.True(pin.HasColumnSetPreview);
        Assert.Equal(3, pin.ColumnSetPreviewItems.Count);

        ColumnSetPreviewItem first = pin.ColumnSetPreviewItems[0];
        Assert.Equal("id", first.ColumnName);
        Assert.Equal(PinDataType.Integer, first.ScalarType);
        Assert.Equal("INT", first.ScalarLabel);
        Assert.Equal("NOT NULL", first.NullabilityLabel);
    }

    [Fact]
    public void ColumnSetTooltipPreview_WithOverflow_ExposesRemainingCount()
    {
        var cols = Enumerable.Range(1, 8)
            .Select(i => new ColumnRefMeta($"c{i}", "t", PinDataType.Integer, i % 2 == 0))
            .ToList();

        var meta = new ColumnSetMeta(cols);

        var def = new NodeDefinition(
            NodeType.ColumnSetBuilder,
            NodeCategory.DataSource,
            "Column Set Builder",
            "test",
            [new PinDescriptor("result", PinDirection.Output, PinDataType.ColumnSet, IsRequired: false, ColumnSetMeta: meta)],
            []);

        var node = new NodeViewModel(def, new Point(0, 0));
        PinViewModel pin = node.OutputPins.First(p => p.Name == "result");

        Assert.Equal(6, pin.ColumnSetPreviewItems.Count);
        Assert.True(pin.HasColumnSetPreviewOverflow);
        Assert.Equal("+2 more", pin.ColumnSetPreviewOverflowLabel);
    }
}


