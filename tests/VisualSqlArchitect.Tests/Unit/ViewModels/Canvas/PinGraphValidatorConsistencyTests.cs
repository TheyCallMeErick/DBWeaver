using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.Services.Validation;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public sealed class PinGraphValidatorConsistencyTests
{
    [Fact]
    public void Validate_WhenScalarPinsAreIncompatible_EmitsDomainReasonCodeIssue()
    {
        using var canvas = new CanvasViewModel();

        var sourceNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ValueBoolean), new Point(0, 0));
        var sumNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Sum), new Point(220, 0));

        canvas.Nodes.Add(sourceNode);
        canvas.Nodes.Add(sumNode);

        PinViewModel from = sourceNode.OutputPins.Single(p => p.Name == "result");
        PinViewModel to = sumNode.InputPins.Single(p => p.Name == "value");

        canvas.Connections.Add(new ConnectionViewModel(from, from.AbsolutePosition, to.AbsolutePosition) { ToPin = to });

        IReadOnlyList<ValidationIssue> issues = GraphValidator.Validate(canvas);

        Assert.Contains(issues, issue => issue.Code == "PIN_ScalarTypeMismatch");
    }

    [Fact]
    public void Validate_WhenStructuralPinsAreIncompatible_EmitsStructuralMismatchIssue()
    {
        using var canvas = new CanvasViewModel();

        var sourceNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Subquery), new Point(0, 0));
        var sumNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Sum), new Point(220, 0));

        canvas.Nodes.Add(sourceNode);
        canvas.Nodes.Add(sumNode);

        PinViewModel from = sourceNode.OutputPins.Single(p => p.Name == "result");
        PinViewModel to = sumNode.InputPins.Single(p => p.Name == "value");

        canvas.Connections.Add(new ConnectionViewModel(from, from.AbsolutePosition, to.AbsolutePosition) { ToPin = to });

        IReadOnlyList<ValidationIssue> issues = GraphValidator.Validate(canvas);

        Assert.Contains(issues, issue => issue.Code == "STRUCTURAL_TYPE_MISMATCH");
    }
}
