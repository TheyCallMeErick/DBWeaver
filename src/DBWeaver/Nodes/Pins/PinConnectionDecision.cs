namespace DBWeaver.Nodes.Pins;

public sealed record PinConnectionDecision(
    bool IsAllowed,
    PinConnectionReasonCode ReasonCode,
    string? DiagnosticMessage,
    IReadOnlyList<IPinMutation> Mutations,
    IReadOnlyList<IPinDomainEvent> Events)
{
    public static PinConnectionDecision Allowed(
        PinConnectionReasonCode reasonCode = PinConnectionReasonCode.None,
        string? diagnosticMessage = null,
        IReadOnlyList<IPinMutation>? mutations = null,
        IReadOnlyList<IPinDomainEvent>? events = null) =>
        new(
            true,
            reasonCode,
            diagnosticMessage,
            mutations ?? [],
            events ?? []);

    public static PinConnectionDecision Rejected(
        PinConnectionReasonCode reasonCode,
        string? diagnosticMessage = null) =>
        new(false, reasonCode, diagnosticMessage, [], []);
}
