namespace DBWeaver.UI.Services.Explain;

public sealed record ExplainHistoryState(
    DateTimeOffset TimestampUtc,
    string TopOperation,
    double? TopCost,
    int AlertCount
);
