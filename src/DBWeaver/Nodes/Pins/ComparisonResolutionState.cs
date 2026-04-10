namespace DBWeaver.Nodes.Pins;

public sealed record ComparisonResolutionState(
    string OwnerNodeId,
    PinDataType? ExpectedScalarType,
    bool HasActiveConcretization);
