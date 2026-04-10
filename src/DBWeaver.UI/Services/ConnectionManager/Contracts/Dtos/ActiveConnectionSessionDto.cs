namespace DBWeaver.UI.Services.ConnectionManager.Contracts;

public sealed record ActiveConnectionSessionDto(
    string? ConnectionId,
    ConnectionSessionStateDto SessionState,
    DateTimeOffset? StartedAt,
    string? SessionLabel);
