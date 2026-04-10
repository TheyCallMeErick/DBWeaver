namespace DBWeaver.UI.Services.ConnectionManager;

public interface IConnectionProfileFormMapper
{
    ConnectionProfileFormData CreateNew();

    ConnectionProfileFormData FromProfile(ConnectionProfile profile);

    ConnectionProfile ToProfile(ConnectionProfileFormData formData);
}

