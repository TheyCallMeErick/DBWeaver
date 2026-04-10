namespace DBWeaver.Nodes.Pins;

public sealed record PinConnectionContext(
    PinConnectionContextData Data,
    IReadOnlyDictionary<string, object?> Extensions)
{
    public static PinConnectionContext ValidationOnly() =>
        new(
            new PinConnectionContextData([], new Dictionary<PinId, PinConnectionSnapshot[]>(), true, false, null, null),
            new Dictionary<string, object?>());
}
