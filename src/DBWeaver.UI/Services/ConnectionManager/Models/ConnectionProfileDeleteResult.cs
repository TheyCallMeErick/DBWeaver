namespace DBWeaver.UI.Services.ConnectionManager;

public readonly record struct ConnectionProfileDeleteResult(
    bool Deleted,
    string? RemovedProfileId,
    string? NextActiveProfileId,
    bool IsEditing,
    string TestStatus);

