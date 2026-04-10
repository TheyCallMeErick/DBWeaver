using DBWeaver.Nodes;
using DBWeaver.Nodes.Pins;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class ReportNodeGraphContractTests
{
    [Fact]
    public void NodeDefinitions_ExposeRawSqlAndReportOutputContracts()
    {
        NodeDefinition rawSql = NodeDefinitionRegistry.Get(NodeType.RawSqlQuery);
        NodeDefinition reportOutput = NodeDefinitionRegistry.Get(NodeType.ReportOutput);

        Assert.Equal(NodeCategory.DataSource, rawSql.Category);
        Assert.Contains(rawSql.Pins, p => p.Direction == PinDirection.Output && p.Name == "query" && p.DataType == PinDataType.ReportQuery);
        Assert.Contains(rawSql.Pins, p => p.Direction == PinDirection.Input && p.Name == "sql_text" && p.DataType == PinDataType.Text);

        Assert.Equal(NodeCategory.Output, reportOutput.Category);
        Assert.Contains(reportOutput.Pins, p => p.Direction == PinDirection.Input && p.Name == "query" && p.DataType == PinDataType.ReportQuery);
        Assert.Contains(reportOutput.Pins, p => p.Direction == PinDirection.Output && p.Name == "result" && p.DataType == PinDataType.ReportQuery);
    }

    [Fact]
    public void PinCompatibility_ReportQueryOnlyConnectsToReportQuery()
    {
        PinModel reportSource = CreatePin("raw", "query", PinDirection.Output, PinDataType.ReportQuery, NodeType.RawSqlQuery);
        PinModel reportSink = CreatePin("output", "query", PinDirection.Input, PinDataType.ReportQuery, NodeType.ReportOutput);
        PinModel numericSource = CreatePin("n1", "value", PinDirection.Output, PinDataType.Number, NodeType.ValueNumber);

        PinConnectionDecision validDecision = reportSink.CanConnect(reportSource, PinConnectionContext.ValidationOnly());
        PinConnectionDecision invalidDecision = reportSink.CanConnect(numericSource, PinConnectionContext.ValidationOnly());

        Assert.True(validDecision.IsAllowed);
        Assert.False(invalidDecision.IsAllowed);
        Assert.Equal(PinConnectionReasonCode.ScalarTypeMismatch, invalidDecision.ReasonCode);
    }

    private static PinModel CreatePin(
        string nodeId,
        string pinName,
        PinDirection direction,
        PinDataType dataType,
        NodeType nodeType)
    {
        var descriptor = new PinDescriptor(pinName, direction, dataType);
        var owner = new PinModelOwner(nodeId, nodeType);
        var pinId = new PinId($"{nodeId}:{pinName}:{direction}");
        return PinModelFactory.Create(pinId, descriptor, owner, dataType, null);
    }
}
