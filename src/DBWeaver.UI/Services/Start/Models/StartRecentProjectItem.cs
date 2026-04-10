namespace DBWeaver.UI.Services.Start.Models;

public sealed record StartRecentProjectItem(
    string DisplayName,
    string Provider,
    string LastOpenedLabel,
    string? FilePath = null,
    string? SnapshotSummary = null
);
