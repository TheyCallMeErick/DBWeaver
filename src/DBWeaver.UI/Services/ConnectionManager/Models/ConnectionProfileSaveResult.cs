namespace DBWeaver.UI.Services.ConnectionManager;

public readonly record struct ConnectionProfileSaveResult(
    ConnectionProfile SelectedProfile,
    bool ActiveProfileAffected);

