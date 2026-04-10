using System.Collections.ObjectModel;

namespace DBWeaver.UI.Services.ConnectionManager;

public interface IConnectionProfileLifecycleService
{
    ConnectionProfileSaveResult Save(
        ObservableCollection<ConnectionProfile> profiles,
        ConnectionProfile profile,
        string? activeProfileId);

    ConnectionProfileDeleteResult Delete(
        ObservableCollection<ConnectionProfile> profiles,
        ConnectionProfile? selectedProfile,
        string? activeProfileId);
}

