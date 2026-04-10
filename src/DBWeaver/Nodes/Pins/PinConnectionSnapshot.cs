namespace DBWeaver.Nodes.Pins;

public sealed record PinConnectionSnapshot(
    string ConnectionId,
    PinId SourcePinId,
    PinId DestinationPinId,
    string SourceNodeId,
    string DestinationNodeId,
    string SourcePinName,
    string DestinationPinName,
    PinDataType SourceEffectiveDataType,
    PinDataType DestinationEffectiveDataType,
    PinDataType? SourceResolvedScalarType,
    string? SemanticSourceKey = null);
