using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public sealed record ExplainSnapshot(
    string Label,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ExplainStep> Steps
);
