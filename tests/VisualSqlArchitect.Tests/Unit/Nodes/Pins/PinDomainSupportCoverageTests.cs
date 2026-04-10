using DBWeaver.Nodes;
using DBWeaver.Nodes.Pins;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class PinDomainSupportCoverageTests
{
    [Fact]
    public void InputPinModel_Throws_WhenDescriptorDirectionIsNotInput()
    {
        var descriptor = new PinDescriptor("value", PinDirection.Output, PinDataType.Number);
        var owner = new PinModelOwner("node-1", NodeType.Sum);

        Assert.Throws<ArgumentException>(() => new InputPinModel(
            new PinId("node-1:value:Output"),
            descriptor,
            owner,
            PinDataType.Number,
            null,
            new DefaultCanConnectCapability()));
    }

    [Fact]
    public void InputPinModel_CreatesSuccessfully_WhenDescriptorDirectionIsInput()
    {
        var descriptor = new PinDescriptor("value", PinDirection.Input, PinDataType.Number);
        var owner = new PinModelOwner("node-1", NodeType.Sum);

        var model = new InputPinModel(
            new PinId("node-1:value:Input"),
            descriptor,
            owner,
            PinDataType.Number,
            null,
            new DefaultCanConnectCapability());

        Assert.Equal(PinDirection.Input, model.Direction);
    }

    [Fact]
    public void OutputPinModel_Throws_WhenDescriptorDirectionIsNotOutput()
    {
        var descriptor = new PinDescriptor("value", PinDirection.Input, PinDataType.Number);
        var owner = new PinModelOwner("node-1", NodeType.Sum);

        Assert.Throws<ArgumentException>(() => new OutputPinModel(
            new PinId("node-1:value:Input"),
            descriptor,
            owner,
            PinDataType.Number,
            null,
            new DefaultCanConnectCapability()));
    }

    [Fact]
    public void OutputPinModel_CreatesSuccessfully_WhenDescriptorDirectionIsOutput()
    {
        var descriptor = new PinDescriptor("value", PinDirection.Output, PinDataType.Number);
        var owner = new PinModelOwner("node-1", NodeType.Sum);

        var model = new OutputPinModel(
            new PinId("node-1:value:Output"),
            descriptor,
            owner,
            PinDataType.Number,
            null,
            new DefaultCanConnectCapability());

        Assert.Equal(PinDirection.Output, model.Direction);
    }

    [Fact]
    public void ColumnRefPinModel_ExposesSchemaCapability()
    {
        var meta = new ColumnRefMeta("id", "orders", PinDataType.Integer, false);
        var descriptor = new PinDescriptor("id", PinDirection.Output, PinDataType.ColumnRef, ColumnRefMeta: meta);
        var owner = new PinModelOwner("node-1", NodeType.TableSource);

        var model = new ColumnRefPinModel(
            new PinId("node-1:id:Output"),
            descriptor,
            owner,
            PinDataType.ColumnRef,
            null,
            new DefaultCanConnectCapability());

        Assert.Same(meta, model.ColumnRef);
        Assert.Null(model.ColumnSet);
    }

    [Fact]
    public void ColumnSetPinModel_ExposesSchemaCapability()
    {
        var meta = new ColumnSetMeta([
            new ColumnRefMeta("id", "orders", PinDataType.Integer, false),
        ]);
        var descriptor = new PinDescriptor("set", PinDirection.Output, PinDataType.ColumnSet, ColumnSetMeta: meta);
        var owner = new PinModelOwner("node-1", NodeType.ColumnList);

        var model = new ColumnSetPinModel(
            new PinId("node-1:set:Output"),
            descriptor,
            owner,
            PinDataType.ColumnSet,
            null,
            new DefaultCanConnectCapability());

        Assert.Null(model.ColumnRef);
        Assert.Same(meta, model.ColumnSet);
    }

    [Fact]
    public void SupportRecords_AreMaterializedWithExpectedValues()
    {
        var comparison = new ComparisonResolutionState("cmp-1", PinDataType.Integer, true);
        var wildcard = new WildcardProjectionContext(
            true,
            new HashSet<NodeType> { NodeType.ColumnList },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "columns" });

        Assert.Equal("cmp-1", comparison.OwnerNodeId);
        Assert.Equal(PinDataType.Integer, comparison.ExpectedScalarType);
        Assert.True(comparison.HasActiveConcretization);
        Assert.True(wildcard.IsEnabled);
        Assert.Contains(NodeType.ColumnList, wildcard.AllowedDestinationNodeTypes);
        Assert.Contains("columns", wildcard.AllowedDestinationPinNames);
    }

    [Fact]
    public void MutationReasonCodes_AreStable()
    {
        var clear = new ClearComparisonScalarMutation("cmp");
        var replace = new ReplaceExistingConnectionMutation(new PinId("x"), ["wire-1"]);
        var prune = new PruneConnectionsMutation(["wire-2"], "pruned");

        Assert.Equal(PinConnectionReasonCode.None, clear.ReasonCode);
        Assert.Equal(PinConnectionReasonCode.MultiplicityExceeded, replace.ReasonCode);
        Assert.Equal(PinConnectionReasonCode.WildcardProjectionOnly, prune.ReasonCode);
    }

    [Fact]
    public void PinId_ExposesValue()
    {
        var pinId = new PinId("node-1:value:Input");

        Assert.Equal("node-1:value:Input", pinId.Value);
        Assert.Equal(pinId, new PinId("node-1:value:Input"));
    }
}
