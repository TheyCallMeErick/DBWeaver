using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.UndoRedo.Commands;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public sealed class SetComparisonConcretizationCommandTests
{
    [Fact]
    public void Execute_SetsExpectedScalarType_OnComparisonColumnRefInputs()
    {
        var canvas = new CanvasViewModel();
        var comparison = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Equals), new Point(0, 0));
        canvas.Nodes.Add(comparison);

        var command = new SetComparisonConcretizationCommand(comparison.Id, PinDataType.Number);
        command.Execute(canvas);

        foreach (PinViewModel input in comparison.InputPins.Where(p => p.DataType == PinDataType.ColumnRef && p.ColumnRefMeta is null))
            Assert.Equal(PinDataType.Number, input.ExpectedColumnScalarType);
    }

    [Fact]
    public void Undo_RestoresPreviousExpectedScalarType()
    {
        var canvas = new CanvasViewModel();
        var comparison = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Equals), new Point(0, 0));
        canvas.Nodes.Add(comparison);

        comparison.ComparisonExpectedScalarType = PinDataType.Integer;

        var command = new SetComparisonConcretizationCommand(comparison.Id, PinDataType.Number);
        command.Execute(canvas);
        command.Undo(canvas);

        Assert.Equal(PinDataType.Integer, comparison.ComparisonExpectedScalarType);
        foreach (PinViewModel input in comparison.InputPins.Where(p => p.DataType == PinDataType.ColumnRef && p.ColumnRefMeta is null))
            Assert.Equal(PinDataType.Integer, input.ExpectedColumnScalarType);
    }
}
