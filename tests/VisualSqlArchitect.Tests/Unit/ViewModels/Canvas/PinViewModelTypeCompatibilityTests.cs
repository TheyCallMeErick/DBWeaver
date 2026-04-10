using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class PinViewModelTypeCompatibilityTests
{
    [Fact]
    public void CanAccept_AllowsNumericFamilyCompatibility_IntegerToDecimal()
    {
        var srcNode = new NodeViewModel("public.orders", [("id", PinDataType.Integer)], new Point(0, 0));
        var dstNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Sum), new Point(100, 0));

        PinViewModel src = srcNode.OutputPins.First(p => p.Name == "id");
        PinViewModel dst = dstNode.InputPins.First(p => p.Name == "value");

        Assert.True(dst.CanAccept(src));
    }

    [Fact]
    public void CanAccept_RejectsRowSetToScalarConnection()
    {
        var srcNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Subquery), new Point(0, 0));
        var dstNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Sum), new Point(100, 0));

        PinViewModel src = srcNode.OutputPins.First(p => p.Name == "result");
        PinViewModel dst = dstNode.InputPins.First(p => p.Name == "value");

        Assert.False(dst.CanAccept(src));
    }
}


