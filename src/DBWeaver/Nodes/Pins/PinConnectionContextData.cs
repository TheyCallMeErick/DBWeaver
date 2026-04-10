namespace DBWeaver.Nodes.Pins;

public sealed record PinConnectionContextData(
    IReadOnlyList<PinConnectionSnapshot> ExistingConnections,
    IReadOnlyDictionary<PinId, PinConnectionSnapshot[]> ConnectionsByPin,
    bool IsValidationOnly,
    bool AllowImplicitReplacement,
    ComparisonResolutionState? ComparisonState,
    WildcardProjectionContext? WildcardContext);
