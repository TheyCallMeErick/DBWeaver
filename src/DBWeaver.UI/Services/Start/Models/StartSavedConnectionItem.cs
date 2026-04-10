namespace DBWeaver.UI.Services.Start.Models;

public sealed record StartSavedConnectionItem(
    string Id,
    string Name,
    string Provider,
    string StatusLabel,
    bool IsConnected
);
