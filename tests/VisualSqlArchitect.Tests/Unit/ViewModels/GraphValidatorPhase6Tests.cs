using Avalonia;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels;

public class GraphValidatorPhase6Tests
{
    [Fact]
    public void Validate_IncludesClearPinTypeMismatchMessage_ForNonStructuralIncompatibility()
    {
        var canvas = new CanvasViewModel();
        var table = new NodeViewModel("public.orders", [("id", PinDataType.Integer)], new Point(0, 0));
        var equals = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Equals), new Point(260, 0));

        canvas.Nodes.Add(table);
        canvas.Nodes.Add(equals);

        PinViewModel from = table.OutputPins.First(p => p.Name == "*"); // ColumnSet
        PinViewModel to = equals.InputPins.First(p => p.Name == "left"); // ColumnRef

        // Inject invalid connection bypassing UI guards.
        canvas.Connections.Add(new ConnectionViewModel(from, default, default) { ToPin = to });

        var issues = GraphValidator.Validate(canvas);

        Assert.Contains(issues, i =>
            i.Code == "PIN_TYPE_MISMATCH"
            && i.Message.Contains("Cannot connect", StringComparison.OrdinalIgnoreCase)
            && i.Message.Contains("ColumnSet", StringComparison.OrdinalIgnoreCase)
            && i.Message.Contains("ColumnRef", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WarnsOnUnjustifiedExpressionFlow_IntoColumnRef()
    {
        var canvas = new CanvasViewModel();
        var table = new NodeViewModel("public.orders", [("id", PinDataType.Integer)], new Point(0, 0));
        var cast = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Cast), new Point(260, 0));
        var equals = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Equals), new Point(520, 0));

        canvas.Nodes.Add(table);
        canvas.Nodes.Add(cast);
        canvas.Nodes.Add(equals);

        canvas.ConnectPins(table.OutputPins.First(p => p.Name == "id"), cast.InputPins.First(p => p.Name == "value"));
        canvas.ConnectPins(cast.OutputPins.First(p => p.Name == "result"), equals.InputPins.First(p => p.Name == "left"));

        var issues = GraphValidator.Validate(canvas);

        Assert.Contains(issues, i =>
            i.Code == "UNJUSTIFIED_EXPRESSION_PIN"
            && i.Severity == IssueSeverity.Warning);
    }
}
