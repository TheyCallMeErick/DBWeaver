using Avalonia;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Services.Localization;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.PropertyPanel;

public class PropertyPanelAliasVisibilityTests
{
    [Fact]
    public void ShowAliasEditor_IsTrue_ForTransformNode()
    {
        var panel = CreatePanel();
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Upper), new Point(0, 0));

        panel.ShowNode(node);

        Assert.True(panel.ShowAliasEditor);
        Assert.Equal(LocalizationService.Instance["property.outputAlias"], panel.NodeAliasLabel);
    }

    [Fact]
    public void ShowAliasEditor_IsTrue_ForTableSourceNode()
    {
        var panel = CreatePanel();
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Integer)], new Point(0, 0));

        panel.ShowNode(node);

        Assert.True(panel.ShowAliasEditor);
        Assert.Equal(LocalizationService.Instance["property.sourceAlias"], panel.NodeAliasLabel);
    }

    [Fact]
    public void ShowAliasEditor_IsFalse_ForComparisonNode()
    {
        var panel = CreatePanel();
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Between), new Point(0, 0));

        panel.ShowNode(node);

        Assert.False(panel.ShowAliasEditor);
    }

    [Fact]
    public void ShowAliasEditor_IsFalse_ForOutputNode()
    {
        var panel = CreatePanel();
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ResultOutput), new Point(0, 0));

        panel.ShowNode(node);

        Assert.False(panel.ShowAliasEditor);
    }

    private static PropertyPanelViewModel CreatePanel()
    {
        var canvas = new CanvasViewModel();
        var undo = new UndoRedoStack(canvas);
        return new PropertyPanelViewModel(undo);
    }
}
