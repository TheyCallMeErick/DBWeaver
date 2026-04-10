using DBWeaver.Core;
using DBWeaver.Ddl;
using DBWeaver.Nodes;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class SequenceDefinitionGraphFirstContractTests
{
    [Fact]
    public void SequenceDefinition_Definition_ExposesOptionalValueInputs()
    {
        NodeDefinition definition = NodeDefinitionRegistry.Get(NodeType.SequenceDefinition);

        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Input && p.Name == "start_value" && p.DataType == PinDataType.Number);
        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Input && p.Name == "increment" && p.DataType == PinDataType.Number);
        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Input && p.Name == "min_value" && p.DataType == PinDataType.Number);
        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Input && p.Name == "max_value" && p.DataType == PinDataType.Number);
        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Input && p.Name == "cycle" && p.DataType == PinDataType.Boolean);
        Assert.Contains(definition.Pins, p => p.Direction == PinDirection.Input && p.Name == "cache" && p.DataType == PinDataType.Number);
    }

    [Fact]
    public void CompileSequenceDefinition_UsesWiredValues_WhenProvided()
    {
        NodeGraph graph = new()
        {
            Nodes =
            [
                new("out", NodeType.CreateSequenceOutput, new Dictionary<string, string>(), new Dictionary<string, string>()),
                new("seq", NodeType.SequenceDefinition, new Dictionary<string, string>(), new Dictionary<string, string>
                {
                    ["Schema"] = "public",
                    ["SequenceName"] = "order_seq",
                    ["StartValue"] = "1",
                    ["Increment"] = "1",
                    ["MinValue"] = "0",
                    ["MaxValue"] = "999",
                    ["Cycle"] = "false",
                    ["Cache"] = "16",
                }),
                new("start", NodeType.ValueNumber, new Dictionary<string, string>(), new Dictionary<string, string> { ["value"] = "10" }),
                new("inc", NodeType.ValueNumber, new Dictionary<string, string>(), new Dictionary<string, string> { ["value"] = "5" }),
                new("min", NodeType.ValueNumber, new Dictionary<string, string>(), new Dictionary<string, string> { ["value"] = "3" }),
                new("max", NodeType.ValueNumber, new Dictionary<string, string>(), new Dictionary<string, string> { ["value"] = "5000" }),
                new("cycle", NodeType.ValueBoolean, new Dictionary<string, string>(), new Dictionary<string, string> { ["value"] = "true" }),
                new("cache", NodeType.ValueNumber, new Dictionary<string, string>(), new Dictionary<string, string> { ["value"] = "64" }),
            ],
            Connections =
            [
                new("seq", "seq", "out", "seq"),
                new("start", "result", "seq", "start_value"),
                new("inc", "result", "seq", "increment"),
                new("min", "result", "seq", "min_value"),
                new("max", "result", "seq", "max_value"),
                new("cycle", "result", "seq", "cycle"),
                new("cache", "result", "seq", "cache"),
            ],
        };

        DdlCompileResult result = new DdlGraphCompiler(graph, DatabaseProvider.Postgres).CompileWithDiagnostics();
        CreateSequenceExpr expression = Assert.IsType<CreateSequenceExpr>(Assert.Single(result.Statements));

        Assert.Equal(10, expression.StartValue);
        Assert.Equal(5, expression.Increment);
        Assert.Equal(3, expression.MinValue);
        Assert.Equal(5000, expression.MaxValue);
        Assert.True(expression.Cycle);
        Assert.Equal(64, expression.Cache);
    }
}
