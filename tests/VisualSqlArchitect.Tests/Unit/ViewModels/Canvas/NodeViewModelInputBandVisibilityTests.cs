using Avalonia;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class NodeViewModelInputBandVisibilityTests
{
    [Fact]
    public void ShowStandardInputBand_IsFalse_ForTableSourceWithoutInputs()
    {
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));

        Assert.True(node.ShowStandardPins);
        Assert.Empty(node.InputPins);
        Assert.False(node.ShowStandardInputBand);
        Assert.True(node.ShouldShowNoInputsPlaceholder);
    }

    [Fact]
    public void ShowStandardInputBand_IsTrue_ForStandardNodeWithInputs()
    {
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Upper), new Point(0, 0));

        Assert.True(node.ShowStandardPins);
        Assert.NotEmpty(node.InputPins);
        Assert.True(node.ShowStandardInputBand);
    }

    [Fact]
    public void ShowStandardInputBand_Updates_WhenInputPinsCollectionChanges()
    {
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Upper), new Point(0, 0));

        Assert.True(node.ShowStandardInputBand);

        node.InputPins.Clear();
        Assert.False(node.ShowStandardInputBand);

        node.InputPins.Add(
            new PinViewModel(
                new PinDescriptor("text", PinDirection.Input, PinDataType.Text, IsRequired: true),
                node
            )
        );

        Assert.True(node.ShowStandardInputBand);
    }
}
