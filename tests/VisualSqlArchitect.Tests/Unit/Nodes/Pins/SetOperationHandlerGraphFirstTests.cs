using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.QueryEngine;
using DBWeaver.UI.Services.QueryPreview;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class SetOperationHandlerGraphFirstTests
{
    [Fact]
    public void ResolveSetOperation_ConnectedNode_PrefersTextInputsOverLegacyParameters()
    {
        using var canvas = new CanvasViewModel();
        NodeViewModel result = CreateNode(NodeType.ResultOutput, 420, 0);
        NodeViewModel setNode = CreateNode(NodeType.SetOperation, 220, 0);
        setNode.Parameters["operator"] = "UNION";
        setNode.Parameters["query"] = "SELECT legacy_query";

        NodeViewModel opText = CreateValueStringNode("EXCEPT", 20, -20);
        NodeViewModel queryText = CreateValueStringNode("SELECT 42", 20, 20);

        canvas.Nodes.Add(setNode);
        canvas.Nodes.Add(opText);
        canvas.Nodes.Add(queryText);
        canvas.Nodes.Add(result);

        canvas.ConnectPins(setNode.OutputPins.Single(p => p.Name == "result"), result.InputPins.Single(p => p.Name == "set_operation"));
        canvas.ConnectPins(opText.OutputPins.Single(p => p.Name == "result"), setNode.InputPins.Single(p => p.Name == "operator_text"));
        canvas.ConnectPins(queryText.OutputPins.Single(p => p.Name == "result"), setNode.InputPins.Single(p => p.Name == "query_text"));

        var handler = new SetOperationHandler(canvas);
        (SetOperationDefinition? operation, string? warning) = handler.ResolveSetOperation(result);

        Assert.Null(warning);
        Assert.NotNull(operation);
        Assert.Equal("EXCEPT", operation!.Operator);
        Assert.Equal("SELECT 42", operation.QuerySql);
    }

    [Fact]
    public void ResolveSetOperation_ConnectedNode_RequiresQueryTextInput_WhenLegacyQueryParameterIsPresent()
    {
        using var canvas = new CanvasViewModel();
        NodeViewModel result = CreateNode(NodeType.ResultOutput, 420, 0);
        NodeViewModel setNode = CreateNode(NodeType.SetOperation, 220, 0);
        setNode.Parameters["operator"] = "UNION ALL";
        setNode.Parameters["query"] = "SELECT 100";

        canvas.Nodes.Add(setNode);
        canvas.Nodes.Add(result);

        canvas.ConnectPins(setNode.OutputPins.Single(p => p.Name == "result"), result.InputPins.Single(p => p.Name == "set_operation"));

        var handler = new SetOperationHandler(canvas);
        (SetOperationDefinition? operation, string? warning) = handler.ResolveSetOperation(result);

        Assert.Null(operation);
        Assert.NotNull(warning);
        Assert.Contains("query is empty", warning!, StringComparison.OrdinalIgnoreCase);
    }

    private static NodeViewModel CreateNode(NodeType type, double x, double y)
    {
        return new NodeViewModel(NodeDefinitionRegistry.Get(type), new Point(x, y));
    }

    private static NodeViewModel CreateValueStringNode(string value, double x, double y)
    {
        NodeViewModel node = CreateNode(NodeType.ValueString, x, y);
        node.Parameters["value"] = value;
        return node;
    }
}
