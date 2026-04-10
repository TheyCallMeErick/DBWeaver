using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.PropertyPanel;

public class PropertyPanelPinPriorityTests
{
    [Fact]
    public void ShowNode_WhenParameterIsDrivenByConnectedInputPin_DisablesInspectorInputAndUsesConnectedValue()
    {
        NodeDefinition definition = FindDefinitionWithMatchingInputAndParameter();
        var node = new NodeViewModel(definition, new Point(120, 80));

        string paramName = definition.Parameters
            .First(p => definition.InputPins.Any(i => string.Equals(i.Name, p.Name, StringComparison.OrdinalIgnoreCase)))
            .Name;
        PinViewModel targetPin = node.InputPins.First(p => string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));

        var source = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ValueString), new Point(40, 40));
        source.Parameters["value"] = "value-from-connected-node";
        var conn = new ConnectionViewModel(source.OutputPins.First(), default, default) { ToPin = targetPin };

        var canvas = new CanvasViewModel();
        var undo = new UndoRedoStack(canvas);
        var panel = new PropertyPanelViewModel(undo, () => new[] { conn });

        panel.ShowNode(node);

        ParameterRowViewModel row = panel.Parameters.First(r => string.Equals(r.Name, paramName, StringComparison.OrdinalIgnoreCase));
        Assert.False(row.IsEditable);
        Assert.Equal("value-from-connected-node", row.Value);
    }

    [Fact]
    public void ShowNode_WhenParameterIsNotDrivenByConnection_KeepsInspectorInputEditable()
    {
        NodeDefinition definition = FindDefinitionWithMatchingInputAndParameter();
        var node = new NodeViewModel(definition, new Point(120, 80));

        string paramName = definition.Parameters
            .First(p => definition.InputPins.Any(i => string.Equals(i.Name, p.Name, StringComparison.OrdinalIgnoreCase)))
            .Name;

        var canvas = new CanvasViewModel();
        var undo = new UndoRedoStack(canvas);
        var panel = new PropertyPanelViewModel(undo, () => Array.Empty<ConnectionViewModel>());

        panel.ShowNode(node);

        ParameterRowViewModel row = panel.Parameters.First(r => string.Equals(r.Name, paramName, StringComparison.OrdinalIgnoreCase));
        Assert.True(row.IsEditable);
    }

    [Fact]
    public void ShowNode_WhenColumnDefinitionTypeDefIsConnected_DisablesDataTypeAndUsesScalarTypeValue()
    {
        var columnDefinition = new NodeViewModel(
            NodeDefinitionRegistry.Get(NodeType.ColumnDefinition),
            new Point(320, 80)
        );
        ParameterRowViewModel dataTypeRow = new(new NodeParameter("DataType", ParameterKind.Text, "INT"), "INT");
        Assert.Equal("INT", dataTypeRow.Value);

        var scalarType = new NodeViewModel(
            NodeDefinitionRegistry.Get(NodeType.ScalarTypeDefinition),
            new Point(40, 40)
        );
        scalarType.Parameters["TypeKind"] = "VARCHAR";
        scalarType.Parameters["Length"] = "512";

        PinViewModel fromPin = scalarType.OutputPins.First(p =>
            string.Equals(p.Name, "type_def", StringComparison.OrdinalIgnoreCase));
        PinViewModel toPin = columnDefinition.InputPins.First(p =>
            string.Equals(p.Name, "type_def", StringComparison.OrdinalIgnoreCase));

        var conn = new ConnectionViewModel(fromPin, default, default) { ToPin = toPin };

        var canvas = new CanvasViewModel();
        var undo = new UndoRedoStack(canvas);
        var panel = new PropertyPanelViewModel(undo, () => new[] { conn });

        panel.ShowNode(columnDefinition);

        ParameterRowViewModel row = panel.Parameters.First(r =>
            string.Equals(r.Name, "DataType", StringComparison.OrdinalIgnoreCase));
        Assert.False(row.IsEditable);
        Assert.Equal("VARCHAR(512)", row.Value);
    }

    private static NodeDefinition FindDefinitionWithMatchingInputAndParameter()
    {
        NodeDefinition? definition = NodeDefinitionRegistry.All
            .FirstOrDefault(d =>
                d.InputPins.Any(input =>
                    d.Parameters.Any(p => string.Equals(p.Name, input.Name, StringComparison.OrdinalIgnoreCase))));

        return definition ?? throw new InvalidOperationException("No node definition with matching input pin and parameter name was found.");
    }
}
