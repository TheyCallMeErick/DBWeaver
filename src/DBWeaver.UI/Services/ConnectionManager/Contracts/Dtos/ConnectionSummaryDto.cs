namespace DBWeaver.UI.Services.ConnectionManager.Contracts;

public sealed record ConnectionSummaryDto(
    string Id,
    string Name,
    string Provider,
    string SummaryText,
    bool IsFavorite,
    bool IsActive,
    DateTimeOffset? LastUsedAt,
    ConnectionTestStatusDto LastTestStatus,
    DateTimeOffset? LastTestAt);
