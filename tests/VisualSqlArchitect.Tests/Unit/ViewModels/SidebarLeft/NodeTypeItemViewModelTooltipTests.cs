using DBWeaver.Nodes;
using DBWeaver.Nodes.PinTypes;
using DBWeaver.UI.Services.Node;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.SidebarLeft;

public class NodeTypeItemViewModelTooltipTests
{
    [Fact]
    public void TooltipPins_AreBuiltFromDefinition_ByDirection()
    {
        NodeDefinition definition = NodeDefinitionRegistry.Get(NodeType.Equals);
        var vm = new NodeTypeItemViewModel(definition, "#FFFFFF", _ => { });

        Assert.True(vm.HasInputPins);
        Assert.True(vm.HasOutputPins);
        Assert.Contains(vm.InputPins, p => p.Name == "left");
        Assert.Contains(vm.InputPins, p => p.Name == "right");
        Assert.Contains(vm.OutputPins, p => p.Name == "result");
    }

    [Fact]
    public void TooltipPins_UsePinTypeColor()
    {
        NodeDefinition definition = NodeDefinitionRegistry.Get(NodeType.Equals);
        var vm = new NodeTypeItemViewModel(definition, "#FFFFFF", _ => { });
        string expected = PinTypeRegistry.GetType(PinDataType.ColumnRef).VisualColorHex;

        Assert.All(vm.InputPins, pin => Assert.Equal(expected, pin.Color));
    }

    [Fact]
    public void TooltipPins_MapsShapeForDdlTableDef()
    {
        NodeDefinition definition = NodeDefinitionRegistry.Get(NodeType.CreateTableOutput);
        var vm = new NodeTypeItemViewModel(definition, "#FFFFFF", _ => { });

        NodePinTooltipItemViewModel tableDefPin = Assert.Single(vm.InputPins, p => p.TypeName == nameof(PinDataType.TableDef));
        Assert.Equal("■", tableDefPin.ShapeGlyph);
        Assert.Equal("Rounded Square", tableDefPin.ShapeName);
    }

    [Fact]
    public void TooltipTags_AreResolvedWithColors()
    {
        NodeDefinition definition = NodeDefinitionRegistry.Get(NodeType.RawSqlQuery);
        var vm = new NodeTypeItemViewModel(definition, "#FFFFFF", _ => { });

        Assert.True(vm.HasTooltipTags);
        Assert.Contains(vm.TooltipTags, t => t.Name == "report");
        Assert.All(vm.TooltipTags, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.BackgroundColor));
            Assert.False(string.IsNullOrWhiteSpace(t.BorderColor));
        });
    }

    [Fact]
    public void CardTags_AreLimitedToTwoItems()
    {
        NodeDefinition definition = NodeDefinitionRegistry.Get(NodeType.ResultOutput);
        var vm = new NodeTypeItemViewModel(definition, "#FFFFFF", _ => { });

        Assert.True(vm.CardTags.Count <= 2);
        Assert.All(vm.CardTags, tag => Assert.Contains(vm.TooltipTags, t => t.Name == tag.Name));
    }

    [Fact]
    public void IconKind_MatchesNodeIconCatalogForDefinitionCategory()
    {
        NodeDefinition definition = NodeDefinitionRegistry.Get(NodeType.Join);
        var vm = new NodeTypeItemViewModel(definition, "#FFFFFF", _ => { });

        Assert.Equal(NodeIconCatalog.GetKindForCategory(definition.Category), vm.IconKind);
    }
}
