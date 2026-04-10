namespace DBWeaver.Nodes.Pins;

public sealed record WildcardProjectionContext(
    bool IsEnabled,
    IReadOnlySet<NodeType> AllowedDestinationNodeTypes,
    IReadOnlySet<string> AllowedDestinationPinNames);
